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
using System.Net;
using System.Threading.Tasks;

namespace Fluorite.WebSocket
{
    public sealed class WebSocketClientTransport : ITransport
    {
        private readonly System.Net.WebSockets.ClientWebSocket webSocket;

        public WebSocketClientTransport() =>
            this.webSocket = new System.Net.WebSockets.ClientWebSocket();

        private static Uri CreateUrl(EndPoint serverEndPoint, bool performSecureConnection) =>
            new Uri($"{(performSecureConnection ? "wss" : "ws")}://{serverEndPoint}/");

        public ValueTask ConnectAsync(string serverAddress, int port, bool performSecureConnection) =>
            new ValueTask(this.webSocket.ConnectAsync(CreateUrl(new DnsEndPoint(serverAddress, port), performSecureConnection), default));

        public ValueTask ConnectAsync(EndPoint serverEndPoint, bool performSecureConnection) =>
            new ValueTask(this.webSocket.ConnectAsync(CreateUrl(serverEndPoint, performSecureConnection), default));

        public ValueTask ConnectAsync(Uri serverEndPoint) =>
            new ValueTask(this.webSocket.ConnectAsync(serverEndPoint, default));

        public ValueTask DisconnectAsync() =>
            new ValueTask(this.webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "", default));

        public ValueTask SendAsync(ArraySegment<byte> data) =>
            new ValueTask(this.webSocket.SendAsync(data, System.Net.WebSockets.WebSocketMessageType.Binary, false, default));

        public IDisposable Subscribe(IObserver<ArraySegment<byte>> observer)
        {
            throw new NotImplementedException();
        }
    }
}
