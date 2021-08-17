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
using Fluorite.Json;
using Fluorite.Transport;
using Fluorite.WebSockets;
using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite
{
    public static class NestFactoryExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Nest Create(
            this NestFactory _, NestSettings settings) =>
            _.Create(settings, StaticProxyFactory.Instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Nest Create(
            this NestFactory _, ITransport transport) =>
            _.Create(NestSettings.Create(JsonSerializer.Instance, transport));

        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, string serverAddress, int port, bool performSecureConnection, params IHost[] registeringObjects)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            await transport.ConnectAsync(serverAddress, port, performSecureConnection).ConfigureAwait(false);
            return nest;
        }

        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, EndPoint serverEndPoint, bool performSecureConnection, params IHost[] registeringObjects)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            await transport.ConnectAsync(serverEndPoint, performSecureConnection).ConfigureAwait(false);
            return nest;
        }

        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, Uri serverEndPoint, params IHost[] registeringObjects)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            await transport.ConnectAsync(serverEndPoint).ConfigureAwait(false);
            return nest;
        }

        public static Nest StartServer(
            this NestFactory _, int serverPort, bool requiredSecureConnection, params IHost[] registeringObjects)
        {
            var transport = WebSocketServerTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            transport.Start(serverPort, requiredSecureConnection);
            return nest;
        }

        public static Nest StartServer(
            this NestFactory _, string listenEndPointUrl, params IHost[] registeringObjects)
        {
            var transport = WebSocketServerTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            transport.Start(listenEndPointUrl);
            return nest;
        }
    }
}
