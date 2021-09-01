////////////////////////////////////////////////////////////////////////////
//
// Fluorite - Simplest and fully-customizable RPC standalone infrastructure.
// Copyright (c) 2021 Kouji Matsui (@kozy_kekyo, @kekyo2)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using Fluorite.Internal;
using Fluorite.Proxy;
using Fluorite.Serialization;
using Fluorite.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite
{
    /// <summary>
    /// Fluorite Nest class.
    /// </summary>
    /// <remarks>It's core structure of Fluorite.</remarks>
    public sealed class Nest
    {
        private readonly ISerializer serializer;
        private readonly IPeerProxyFactory factory;

        private readonly Dictionary<string, MethodStub> stubs = new();
        private readonly Dictionary<Guid, Awaiter> awaiters = new();

        private ITransport? transport;

        ///////////////////////////////////////////////////////////////////////
        // Constructor

        internal Nest(NestSettings settings, IPeerProxyFactory factory)
        {
            this.factory = factory;
            this.serializer = settings.Serializer;
            this.transport = settings.Transport;

            this.transport.SetPayloadContentType(this.serializer.PayloadContentType);
            this.transport.RegisterReceiver(this.OnReceivedAsync);
        }

        ///////////////////////////////////////////////////////////////////////
        // Shutdown sequence

        public async ValueTask ShutdownAsync()
        {
            if (this.transport != null)
            {
                lock (this.stubs)
                {
                    this.stubs.Clear();
                }

                lock (this.awaiters)
                {
                    foreach (var entry in this.awaiters)
                    {
                        entry.Value.SetCanceled();
                    }

                    this.awaiters.Clear();
                }

                await this.transport.ShutdownAsync().
                    ConfigureAwait(false);

                this.transport.UnregisterReceiver(this.OnReceivedAsync);
                this.transport = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // Register host object

        public void Register(IHost host) =>
            this.Register(host, SynchronizationContext.Current);

        public void Register(IHost host, SynchronizationContext? synchContext)
        {
            lock (this.stubs)
            {
                foreach (var type in host.GetType().GetInterfaces().
                    Where(it => typeof(IHost).IsAssignableFrom(it)))
                {
                    foreach (var method in type.GetMethods())
                    {
                        var identity = ProxyUtilities.GetMethodIdentity(type, method.Name);
                        this.stubs.Add(identity, MethodStub.Create(host, method, synchContext));
                    }
                }
            }
        }

        public void Unregister(IHost host)
        {
            lock (this.stubs)
            {
                foreach (var type in host.GetType().GetInterfaces().
                    Where(it => typeof(IHost).IsAssignableFrom(it)))
                {
                    foreach (var method in type.GetMethods())
                    {
                        var name = ProxyUtilities.GetMethodIdentity(type, method.Name);
                        this.stubs.Remove(name);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // Proxy generator

        public TPeer GetPeer<TPeer>()
            where TPeer : class, IHost =>
            this.factory?.CreateInstance<TPeer>(this)!;

        ///////////////////////////////////////////////////////////////////////
        // RPC management
 
        /// <summary>
        /// Lower level entry point from proxy interface.
        /// </summary>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="methodIdentity">Method (or status) identity</param>
        /// <param name="args">Additional arguments</param>
        /// <returns>Result value</returns>
        internal async ValueTask<TResult> InvokeAsync<TResult>(string methodIdentity, object[] args)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(methodIdentity));
            Debug.Assert(args != null);

            var requestIdentity = Guid.NewGuid();

            var data = await this.serializer.SerializeAsync(requestIdentity, methodIdentity, args!);

            var awaiter = new Awaiter<TResult>();
            lock (this.awaiters)
            {
                this.awaiters.Add(requestIdentity, awaiter);
            }

            try
            {
                await this.transport!.SendAsync(data).
                    ConfigureAwait(false);
            }
            catch
            {
                lock (this.awaiters)
                {
                    this.awaiters.Remove(requestIdentity);
                }
                throw;
            }

            return await awaiter.Task.
                ConfigureAwait(false);
        }

        /// <summary>
        /// Received awaiter calling (on caller role).
        /// </summary>
        /// <param name="container">Received payload container</param>
        /// <param name="awaiter">Awaiter</param>
        private static async ValueTask AcceptAwaiterAsync(
            IPayloadContainerView container, Awaiter awaiter)
        {
            Debug.Assert(container.DataCount == 1);

            if (container.MethodIdentity == "Exception")
            {
                var message = await container.DeserializeDataAsync(0, typeof(string)).
                    ConfigureAwait(false);
                awaiter.SetException((message != null) ? new Exception((string)message) : new Exception());
            }
            else
            {
                Debug.Assert(container.MethodIdentity == "Result");

                try
                {
                    var result = await container.DeserializeDataAsync(0, awaiter.ResultType).
                        ConfigureAwait(false);
                    awaiter.SetResult(result);
                }
                catch (Exception ex)
                {
                    awaiter.SetException(ex);
                }
            }
        }

        /// <summary>
        /// Received calling request (on callee role).
        /// </summary>
        /// <param name="container">Received payload container</param>
        private async ValueTask AcceptInvokingAsync(IPayloadContainerView container)
        {
            MethodStub? hostMethod = null;
            lock (this.stubs)
            {
                this.stubs.TryGetValue(container.MethodIdentity, out hostMethod);
            }

            if (hostMethod != null)
            {
                object? result;
                string name;
                try
                {
                    result = await hostMethod.InvokeAsync(container);
                    name = "Result";
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                    name = "Exception";
                }

                var resultData = await this.serializer.SerializeAsync(container.RequestIdentity, name, new[] { result });
                await this.transport!.SendAsync(resultData).
                    ConfigureAwait(false);
            }
            // Will ignore sprious (Already abandoned awaiter)
            else if ((container.MethodIdentity != "Result") && (container.MethodIdentity != "Exception"))
            {
                var resultData = await this.serializer.SerializeAsync(container.RequestIdentity, "Exception", new[] { "Method not found." });
                await this.transport!.SendAsync(resultData).
                    ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Arrived raw data at transport.
        /// </summary>
        /// <param name="data">Raw data</param>
        /// <returns></returns>
        internal async ValueTask OnReceivedAsync(ArraySegment<byte> data)
        {
            var container = await this.serializer.DeserializeAsync(data);

            Awaiter? awaiter = null;
            lock (this.awaiters)
            {
                if (this.awaiters.TryGetValue(container.RequestIdentity, out awaiter))
                {
                    this.awaiters.Remove(container.RequestIdentity);
                }
            }

            if (awaiter != null)
            {
                await AcceptAwaiterAsync(container, awaiter).
                    ConfigureAwait(false);
            }
            else
            {
                await this.AcceptInvokingAsync(container).
                    ConfigureAwait(false);
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // Factory accessor

        /// <summary>
        /// Factory accessor.
        /// </summary>
        public static readonly NestFactory Factory =
            new NestFactory();
    }
}
