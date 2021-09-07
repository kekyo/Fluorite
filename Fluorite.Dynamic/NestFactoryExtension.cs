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
using Fluorite.Internal;
using Fluorite.Json;
using Fluorite.Proxy;
using Fluorite.Transport;
using Fluorite.WebSockets;
using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#pragma warning disable 1573

namespace Fluorite
{
    public static class NestFactoryExtension
    {
        /// <summary>
        /// Initialize Fluorite.
        /// </summary>
        public static void Initialize(this NestFactory _) =>
            ProxyUtilities.MarkInitialized();

        /// <summary>
        /// Create the nest by defaulted setting.
        /// </summary>
        /// <param name="settings">Nest setting</param>
        /// <returns>Nest</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Nest Create(
            this NestFactory _, NestSettings settings) =>
            _.Create(settings, DynamicProxyFactory.Instance);

        /// <summary>
        /// Create the nest with explicitly transport.
        /// </summary>
        /// <param name="transport">Transport</param>
        /// <returns>Nest</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Nest Create(
            this NestFactory _, ITransport transport) =>
            _.Create(NestSettings.Create(JsonSerializer.Instance, transport));

        /// <summary>
        /// Create the nest and connect WebSocket server.
        /// </summary>
        /// <param name="serverAddress">WebSocket server address</param>
        /// <param name="serverPort">WebSocket port</param>
        /// <param name="performSecureConnection">Perform secure connection when available</param>
        /// <param name="registeringObjects">Expose objects</param>
        /// <returns>Nest</returns>
        public static async ValueTask<Nest> ConnectAsync(
            this NestFactory _, string serverAddress, int serverPort, bool performSecureConnection, params IHost[] registeringObjects)
        {
            var transport = WebSocketClientTransport.Create();
            var nest = _.Create(transport);
            foreach (var registeringObject in registeringObjects)
            {
                nest.Register(registeringObject);
            }
            await transport.ConnectAsync(serverAddress, serverPort, performSecureConnection).ConfigureAwait(false);
            return nest;
        }

        /// <summary>
        /// Create the nest and connect WebSocket server.
        /// </summary>
        /// <param name="serverEndPoint">WebSocket server endpoint</param>
        /// <param name="performSecureConnection">Perform secure connection when available</param>
        /// <param name="registeringObjects">Expose objects</param>
        /// <returns>Nest</returns>
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

        /// <summary>
        /// Create the nest and connect WebSocket server.
        /// </summary>
        /// <param name="serverEndPoint">WebSocket server endpoint</param>
        /// <param name="registeringObjects">Expose objects</param>
        /// <returns>Nest</returns>
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

        /// <summary>
        /// Create the nest and produce WebSocket server.
        /// </summary>
        /// <param name="serverPort">WebSocket server port</param>
        /// <param name="requiredSecureConnection">Will accept when secure connection is enabled</param>
        /// <param name="registeringObjects">Expose objects</param>
        /// <returns>Nest</returns>
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

        /// <summary>
        /// Create the nest and produce WebSocket server.
        /// </summary>
        /// <param name="listenEndPointUrl">WebSocket server endpoint</param>
        /// <param name="registeringObjects">Expose objects</param>
        /// <returns>Nest</returns>
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
