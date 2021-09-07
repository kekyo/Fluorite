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
using System.IO;
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
        private readonly Dictionary<Guid, InvokingAwaiter> awaiters = new();

        private ITransport? transport;

        ///////////////////////////////////////////////////////////////////////
        // Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">Nest setting</param>
        /// <param name="factory">Proxy factory interface</param>
        internal Nest(NestSettings settings, IPeerProxyFactory factory)
        {
            this.factory = factory;
            this.serializer = settings.Serializer;
            this.transport = settings.Transport;

            this.transport.SetPayloadContentType(this.serializer.PayloadContentType);
            this.transport.Initialize(this.OnReceivedAsync);
        }

        ///////////////////////////////////////////////////////////////////////
        // Shutdown sequence

        /// <summary>
        /// Perform shutdown.
        /// </summary>
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
                this.transport = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // Register host object

        /// <summary>
        /// Register exposing object.
        /// </summary>
        /// <param name="host">Expose object</param>
        /// <param name="synchContext">Will be bound synchronization context</param>
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

        /// <summary>
        /// Register exposing object.
        /// </summary>
        /// <param name="host">Expose object</param>
        public void Register(IHost host) =>
            this.Register(host, SynchronizationContext.Current);


        /// <summary>
        /// Unregister exposing object.
        /// </summary>
        /// <param name="host">Expose object</param>
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

        /// <summary>
        /// Get transparent proxy instance by expose interface type.
        /// </summary>
        /// <typeparam name="TPeer">Expose interface type</typeparam>
        /// <returns>Proxy instance</returns>
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

            var awaiter = new InvokingAwaiter<TResult>();
            lock (this.awaiters)
            {
                this.awaiters.Add(requestIdentity, awaiter);
            }

            try
            {
                using (var stream = await this.transport!.GetSenderStreamAsync().
                    ConfigureAwait(false))
                {
                    await this.serializer.SerializeAsync(stream, requestIdentity, methodIdentity, args!).
                        ConfigureAwait(false);
                    await stream.FlushAsync().
                        ConfigureAwait(false);
                }
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
            IPayloadContainerView container, InvokingAwaiter awaiter)
        {
            Debug.Assert(container.BodyCount == 1);

            if (container.MethodIdentity == "Exception")
            {
                var message = await container.DeserializeBodyAsync(0, typeof(string)).
                    ConfigureAwait(false);
                awaiter.SetException((message != null) ? new Exception((string)message) : new Exception());
            }
            else
            {
                Debug.Assert(container.MethodIdentity == "Result");

                try
                {
                    var result = await container.DeserializeBodyAsync(0, awaiter.ResultType).
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
                    result = await hostMethod.InvokeAsync(container).
                        ConfigureAwait(false);
                    name = "Result";
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                    name = "Exception";
                }

                using (var stream = await this.transport!.GetSenderStreamAsync().
                    ConfigureAwait(false))
                {
                    await this.serializer.SerializeAsync(stream, container.RequestIdentity, name, new[] { result }).
                        ConfigureAwait(false);
                    await stream.FlushAsync().
                        ConfigureAwait(false);
                }
            }
            // Will ignore sprious (Already abandoned awaiter)
            else if ((container.MethodIdentity != "Result") && (container.MethodIdentity != "Exception"))
            {
                using (var stream = await this.transport!.GetSenderStreamAsync().
                    ConfigureAwait(false))
                {
                    await this.serializer.SerializeAsync(stream, container.RequestIdentity, "Exception", new[] { "Method not found." }).
                        ConfigureAwait(false);
                    await stream.FlushAsync().
                        ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Arrived raw data at transport.
        /// </summary>
        /// <param name="readFrom">Raw data contained stream</param>
        private async ValueTask OnReceivedAsync(Stream readFrom)
        {
            var container = await this.serializer.DeserializeAsync(readFrom).
                ConfigureAwait(false);

            InvokingAwaiter? awaiter = null;
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
        /// Fluorite factory accessor.
        /// </summary>
        public static readonly NestFactory Factory =
            new NestFactory();
    }
}
