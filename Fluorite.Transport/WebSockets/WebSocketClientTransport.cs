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
    /// <summary>
    /// Fluorite WebSocket transport implementation for client side.
    /// </summary>
    public sealed class WebSocketClientTransport : TransportBase
    {
        private const int BufferElementSize = 16384;

        private TaskCompletionSource<bool>? shutdown;
        private TaskCompletionSource<bool>? done;
        private ClientWebSocket? webSocket;
        private WebSocketController? controller;
        private WebSocketMessageType messageType;

        /// <summary>
        /// Constructor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private WebSocketClientTransport()
        {
        }

        /// <summary>
        /// Calling when set payload content type.
        /// </summary>
        /// <param name="contentType">HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</param>
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

        /// <summary>
        /// Connect WebSocket server.
        /// </summary>
        /// <param name="serverAddress">WebSocket server address</param>
        /// <param name="serverPort">WebSocket port</param>
        /// <param name="performSecureConnection">Perform secure connection when available</param>
        public async ValueTask ConnectAsync(
            string serverAddress, int serverPort, bool performSecureConnection)
        {
            var entry = await Dns.GetHostEntryAsync(serverAddress);
            await this.ConnectAsync(
                CreateUrl(new IPEndPoint(entry.AddressList[0], serverPort), performSecureConnection));
        }

        /// <summary>
        /// Connect WebSocket server.
        /// </summary>
        /// <param name="serverEndPoint">WebSocket server endpoint</param>
        /// <param name="performSecureConnection">Perform secure connection when available</param>
        public ValueTask ConnectAsync(
            EndPoint serverEndPoint, bool performSecureConnection) =>
            this.ConnectAsync(CreateUrl(serverEndPoint, performSecureConnection));

        /// <summary>
        /// Connect WebSocket server.
        /// </summary>
        /// <param name="serverEndPoint">WebSocket server endpoint</param>
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

        /// <summary>
        /// Calling when shutdown sequence.
        /// </summary>
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

        /// <summary>
        /// Calling when get sender stream.
        /// </summary>
        /// <returns>Stream</returns>
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

        /// <summary>
        /// Create WebSocket transport instance.
        /// </summary>
        /// <returns>WebSocketClientTransport</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketClientTransport Create() =>
            new WebSocketClientTransport();
    }
}
