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

using Fluorite.Transport;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    public sealed class WebSocketClientTransport : TransportBase
    {
        private const int BufferElementSize = 16384;

        private TaskCompletionSource<bool>? shutdown;
        private TaskCompletionSource<bool>? done;
        private ClientWebSocket? webSocket;
        private WebSocketController? controller;
        private WebSocketMessageType messageType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private WebSocketClientTransport()
        {
        }

        protected override void OnSetPayloadContentType(string contentType) =>
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
                using (this.controller = new WebSocketController(
                    this.webSocket!, this.messageType, BufferElementSize))
                {
                    try
                    {
                        var shutdownTask = this.shutdown!.Task;
                        await this.controller.RunAsync(this.OnReceivedAsync, shutdownTask).
                            ConfigureAwait(false); ;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    finally
                    {
                        this.webSocket!.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                this.controller = null;
                this.done!.TrySetResult(true);
            }
        }

        private static Uri CreateUrl(EndPoint serverEndPoint, bool performSecureConnection) =>
            new Uri($"{(performSecureConnection ? "wss" : "ws")}://{serverEndPoint}/");

        public async ValueTask ConnectAsync(string serverAddress, int port, bool performSecureConnection)
        {
            var entry = await Dns.GetHostEntryAsync(serverAddress);
            await this.ConnectAsync(
                CreateUrl(new IPEndPoint(entry.AddressList[0], port), performSecureConnection));
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

            await this.webSocket.ConnectAsync(serverEndPoint, default).
                ConfigureAwait(false);

            this.ProceedReceivingAsynchronously();
        }

        protected override async ValueTask OnShutdownAsync()
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

        protected override ValueTask<Stream> OnGetSenderStreamAsync()
        {
            if (this.controller is { } controller)
            {
                var bridge = controller.AllocateSenderStreamBridge();
                return new ValueTask<Stream>(new SenderStream(new[] { bridge }));
            }
            else
            {
                throw new ObjectDisposedException("WebSocketClientTransport already shutdowned.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketClientTransport Create() =>
            new WebSocketClientTransport();
    }
}
