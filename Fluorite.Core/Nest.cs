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
using System.Threading.Tasks;

namespace Fluorite
{
    public sealed class Nest
    {
        private readonly ISerializer serializer;
        private readonly IPeerProxyFactory factory;

        private readonly Dictionary<string, HostMethod> hostMethods = new();
        private readonly Dictionary<Guid, Awaiter> awaits = new();

        private ITransport? transport;
        private IDisposable? disposer;

        internal Nest(NestSettings settings, IPeerProxyFactory factory)
        {
            this.factory = factory;
            this.serializer = settings.Serializer;
            this.transport = settings.Transport;

            this.transport.SetPayloadContentType(this.serializer.PayloadContentType);
            this.disposer = this.transport.Subscribe(new TransportObserver(this));
        }

        public async ValueTask ShutdownAsync()
        {
            if (this.disposer != null)
            {
                lock (this.hostMethods)
                {
                    this.hostMethods.Clear();
                }

                this.CancelAwaitings();

                this.disposer.Dispose();
                this.disposer = null;
            }

            if (this.transport != null)
            {
                await this.transport.ShutdownAsync().
                    ConfigureAwait(false);
                this.transport = null;
            }
        }

        private void CancelAwaitings()
        {
            lock (this.awaits)
            {
                foreach (var entry in this.awaits)
                {
                    entry.Value.SetCanceled();
                }

                this.awaits.Clear();
            }
        }

        public void Register(IHost host)
        {
            lock (this.hostMethods)
            {
                foreach (var type in host.GetType().GetInterfaces().
                    Where(it => typeof(IHost).IsAssignableFrom(it)))
                {
                    foreach (var method in type.GetMethods())
                    {
                        var name = $"{type.FullName}.{method.Name}";
                        this.hostMethods.Add(name, HostMethod.Create(host, method));
                    }
                }
            }
        }

        public void Unregister(IHost host)
        {
            lock (this.hostMethods)
            {
                foreach (var type in host.GetType().GetInterfaces().
                    Where(it => typeof(IHost).IsAssignableFrom(it)))
                {
                    foreach (var method in type.GetMethods())
                    {
                        var name = $"{type.FullName}.{method.Name}";
                        this.hostMethods.Remove(name);
                    }
                }
            }
        }

        public TPeer GetPeer<TPeer>()
            where TPeer : class, IHost =>
            this.factory?.CreateInstance<TPeer>(this)!;

        internal async ValueTask<TResult> InvokeAsync<TResult>(string name, object[] args)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(name));
            Debug.Assert(args != null);

            var identity = Guid.NewGuid();

            var data = await this.serializer.SerializeAsync(identity, name, args!).
                ConfigureAwait(false);

            var awaiter = new Awaiter<TResult>();
            lock (this.awaits)
            {
                this.awaits.Add(identity, awaiter);
            }

            try
            {
                await this.transport!.SendAsync(data).
                    ConfigureAwait(false);
            }
            catch
            {
                lock (this.awaits)
                {
                    this.awaits.Remove(identity);
                }
                throw;
            }

            return await awaiter.Task.
                ConfigureAwait(false);
        }

        private static async ValueTask ProceedAwaiterAsync(
            IPayloadContainerView container, Awaiter awaiter)
        {
            Debug.Assert(container.DataCount == 1);

            if (container.Name == "Exception")
            {
                var message = await container.DeserializeDataAsync(0, typeof(string)).
                    ConfigureAwait(false);
                awaiter.SetException((message != null) ? new Exception((string)message) : new Exception());
            }
            else
            {
                Debug.Assert(container.Name == "Result");

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

        private async ValueTask ProceedInvokingAsync(IPayloadContainerView container)
        {
            HostMethod? hostMethod = null;
            lock (this.hostMethods)
            {
                this.hostMethods.TryGetValue(container.Name, out hostMethod);
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

                var resultData = await this.serializer.SerializeAsync(container.Identity, name, new[] { result }).
                    ConfigureAwait(false);
                await this.transport!.SendAsync(resultData).
                    ConfigureAwait(false);
            }
            else
            {
                var resultData = await this.serializer.SerializeAsync(container.Identity, "Exception", new[] { "Method not found." }).
                    ConfigureAwait(false);
                await this.transport!.SendAsync(resultData).
                    ConfigureAwait(false);
            }
        }

        internal async ValueTask OnNextAsync(ArraySegment<byte> data)
        {
            var container = await this.serializer.DeserializeAsync(data).
                ConfigureAwait(false);

            Awaiter? awaiter = null;
            lock (this.awaits)
            {
                if (this.awaits.TryGetValue(container.Identity, out awaiter))
                {
                    this.awaits.Remove(container.Identity);
                }
            }

            if (awaiter != null)
            {
                await ProceedAwaiterAsync(container, awaiter).
                    ConfigureAwait(false);
            }
            else
            {
                await this.ProceedInvokingAsync(container).
                    ConfigureAwait(false);
            }
        }

        internal void OnCompleted() =>
            this.CancelAwaitings();

        public static readonly NestFactory Factory =
            new NestFactory();
    }
}
