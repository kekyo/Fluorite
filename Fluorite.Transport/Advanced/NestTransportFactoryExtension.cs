﻿////////////////////////////////////////////////////////////////////////////
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

using Fluorite.WebSocket;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Fluorite.Advanced
{
    public static class NestTransportFactoryExtension
    {
        public static async ValueTask<Nest<TInterface>> ConnectAsync<TInterface>(
            this NestFactory _, string serverAddress, int port, bool performSecureConnection, ISerializer serializer)
        {
            var transport = new WebSocketClientTransport();
            await transport.ConnectAsync(serverAddress, port, performSecureConnection).ConfigureAwait(false);
            return _.Create<TInterface>(NestSettings.Create(serializer, transport));
        }

        public static async ValueTask<Nest<TInterface>> ConnectAsync<TInterface>(
            this NestFactory _, EndPoint serverEndPoint, bool performSecureConnection, ISerializer serializer)
        {
            var transport = new WebSocketClientTransport();
            await transport.ConnectAsync(serverEndPoint, performSecureConnection).ConfigureAwait(false);
            return _.Create<TInterface>(NestSettings.Create(serializer, transport));
        }

        public static async ValueTask<Nest<TInterface>> ConnectAsync<TInterface>(
            this NestFactory _, Uri serverEndPoint, ISerializer serializer)
        {
            var transport = new WebSocketClientTransport();
            await transport.ConnectAsync(serverEndPoint).ConfigureAwait(false);
            return _.Create<TInterface>(NestSettings.Create(serializer, transport));
        }
    }
}
