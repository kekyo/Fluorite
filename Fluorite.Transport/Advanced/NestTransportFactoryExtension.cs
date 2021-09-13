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

using Fluorite.Proxy;
using Fluorite.Serialization;
using Fluorite.WebSockets;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Fluorite.Advanced
{
    public static class NestTransportFactoryExtension
    {
        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, string serverAddress, int port, bool performSecureConnection, ISerializer serializer)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = NestBasisFactory.Create(NestSettings.Create(serializer, transport));
            await transport.ConnectAsync(serverAddress, port, performSecureConnection).ConfigureAwait(false);
            return nest;
        }

        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, EndPoint serverEndPoint, bool performSecureConnection, ISerializer serializer)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = NestBasisFactory.Create(NestSettings.Create(serializer, transport));
            await transport.ConnectAsync(serverEndPoint, performSecureConnection).ConfigureAwait(false);
            return nest;
        }

        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, Uri serverEndPoint, ISerializer serializer)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = NestBasisFactory.Create(NestSettings.Create(serializer, transport));
            await transport.ConnectAsync(serverEndPoint).ConfigureAwait(false);
            return nest;
        }
    }
}
