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
using Fluorite.Transport;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    public sealed class WebSocketClientTransport : TransportBase
    {
        private const int BufferElementSize = 16384;

        private readonly SemaphoreSlim locker = new(1, 1);
        private TaskCompletionSource<bool>? shutdown;
        private TaskCompletionSource<bool>? done;
        private ClientWebSocket? webSocket;
        private WebSocketMessageType messageType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private WebSocketClientTransport()
        {
        }

        protected override void SetPayloadContentType(string contentType) =>
            this.messageType = (contentType == "application/octet-stream") ?
                WebSocketMessageType.Binary :
                WebSocketMessageType.Text;

        private async void ProceedReceivingAsynchronously()
        {
            Debug.Assert(this.webSocket != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);

            try
            {
                var buffer = new ExpandableBuffer(BufferElementSize);

                var receiveTask = this.webSocket!.ReceiveAsync(buffer, default);
                var abortTask = this.shutdown!.Task;

                while (true)
                {
                    var awakeTask = await Task.WhenAny(abortTask, receiveTask).
                        ConfigureAwait(false);
                    if (object.ReferenceEquals(awakeTask, abortTask))
                    {
                        var _ = receiveTask.ContinueWith(_ => { });    // ignoring sink
                        break;
                    }

                    var result = await receiveTask.
                        ConfigureAwait(false);

                    if (result.MessageType == this.messageType)
                    {
                        buffer.Adjust(result.Count);
                        if (result.EndOfMessage)
                        {
                            this.OnReceived(buffer.Extract());
                            buffer = new ExpandableBuffer(BufferElementSize);
                        }
                        else
                        {
                            buffer.Next();
                        }
                    }

                    if (result.CloseStatus != default)
                    {
                        var _ = abortTask.ContinueWith(_ => { });    // ignoring sink
                        break;
                    }

                    receiveTask = this.webSocket!.ReceiveAsync(buffer, default);
                }

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default).
                    ConfigureAwait(false);

                this.OnReceiveFinished();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                this.OnReceiveError(ex);
            }

            this.done!.TrySetResult(true);
        }

        private static Uri CreateUrl(EndPoint serverEndPoint, bool performSecureConnection) =>
            new Uri($"{(performSecureConnection ? "wss" : "ws")}://{serverEndPoint}/");

        public async ValueTask ConnectAsync(string serverAddress, int port, bool performSecureConnection)
        {
            var entry = await Dns.GetHostEntryAsync(serverAddress).
                ConfigureAwait(false);
            await this.ConnectAsync(
                CreateUrl(new IPEndPoint(entry.AddressList[0], port), performSecureConnection)).
                ConfigureAwait(false);
        }

        public ValueTask ConnectAsync(EndPoint serverEndPoint, bool performSecureConnection) =>
            this.ConnectAsync(CreateUrl(serverEndPoint, performSecureConnection));

        public async ValueTask ConnectAsync(Uri serverEndPoint)
        {
            Debug.Assert(this.webSocket == null);
            Debug.Assert(this.shutdown == null);
            Debug.Assert(this.done == null);

            this.shutdown = new();
            this.done = new();
            this.webSocket = new();

            await this.webSocket.ConnectAsync(serverEndPoint, default);

            this.ProceedReceivingAsynchronously();
        }

        public override async ValueTask ShutdownAsync()
        {
            Debug.Assert(this.webSocket != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);

            this.shutdown!.TrySetResult(true);

            try
            {
                await this.done!.Task.
                    ConfigureAwait(false);
            }
            finally
            {
                this.webSocket!.Dispose();
                this.webSocket = null;

                this.shutdown = null;
                this.done = null;
            }
        }

        public override async ValueTask SendAsync(ArraySegment<byte> data)
        {
            await this.locker.WaitAsync().
                ConfigureAwait(false);
            try
            {
                await this.webSocket!.SendAsync(
                    data, this.messageType, true, default).
                    ConfigureAwait(false);
            }
            finally
            {
                this.locker.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketClientTransport Create() =>
            new WebSocketClientTransport();
    }
}
