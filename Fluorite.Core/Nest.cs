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

using Fluorite.Advanced;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite
{
    public sealed class NestSettings
    {
        public readonly ISerializer Serializer;
        public readonly ITransport Transport;

        private NestSettings(ISerializer serializer, ITransport transport)
        {
            this.Serializer = serializer;
            this.Transport = transport;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NestSettings Create(ISerializer serializer, ITransport transport) =>
            new NestSettings(serializer, transport);
    }

    public sealed class NestFactory
    {
        internal NestFactory()
        {
        }
    }

    public abstract class Nest
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected Nest()
        {
        }

        internal abstract ValueTask<TResult> InvokeAsync<TResult>(string name, object[] args);

        public static readonly NestFactory Factory =
            new NestFactory();
    }

    public sealed class Nest<TInterface> : Nest
    {
        static Nest()
        {
            if (!typeof(TInterface).IsInterface)
            {
                throw new ArgumentException($"It isn't interface type: Type={typeof(TInterface).FullName}");
            }
        }

        private readonly ISerializer serializer;
        private readonly ITransport transport;

        internal Nest(NestSettings settings)
        {
            this.Peer = default!;
            this.serializer = settings.Serializer;
            this.transport = settings.Transport;
        }

        internal Nest(NestSettings settings, IProxyFactory<TInterface> factory)
        {
            var proxy = factory.CreateInstance(this);
            this.Peer = proxy;
            this.serializer = settings.Serializer;
            this.transport = settings.Transport;
        }

        public TInterface Peer { get; }

        internal override ValueTask<TResult> InvokeAsync<TResult>(string name, object[] args) =>
            throw new InvalidOperationException();
    }
}
