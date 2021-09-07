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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    /// <summary>
    /// Fluorite WebSocket transport implementation for server side.
    /// </summary>
    public sealed class WebSocketServerTransport :
        TransportBase
    {
        private const int BufferElementSize = 16384;

        private readonly Dictionary<WebSocket, WebSocketController> connections = new();

        private HttpListener? httpListener;
        private TaskCompletionSource<bool>? shutdown;
        private TaskCompletionSource<bool>? done;
        private volatile int concurrentCount;
        private WebSocketMessageType messageType = WebSocketMessageType.Text;

        /// <summary>
        /// Constructor.
        /// </summary>
        private WebSocketServerTransport()
        {
        }

        private async ValueTask PumpAsync(WebSocket webSocket, Task shutdownTask)
        {
            Debug.Assert(this.httpListener != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);

            using (var controller = new WebSocketController(
                webSocket, this.messageType, BufferElementSize))
            {
                lock (this.connections)
                {
                    this.connections.Add(webSocket, controller);
                }

                try
                {
                    await controller.RunAsync(this.OnReceivedAsync, shutdownTask).
                        ConfigureAwait(false);
                }
                finally
                {
                    lock (this.connections)
                    {
                        this.connections.Remove(webSocket);
                    }
                    webSocket.Dispose();
                }
            }
        }

        private async void ConnectAsynchronously(HttpListenerContext httpContext)
        {
            Debug.Assert(this.httpListener != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);
            Debug.Assert(this.concurrentCount >= 1);

            Interlocked.Increment(ref this.concurrentCount);

            try
            {
                if (!httpContext.Request.IsWebSocketRequest)
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.Close();
                    return;
                }

                var acceptWebSocketTask = httpContext.AcceptWebSocketAsync(null!);
                var shutdownTask = this.shutdown!.Task;

                var awakeTask = await Task.WhenAny(acceptWebSocketTask, shutdownTask).
                    ConfigureAwait(false);
                if (object.ReferenceEquals(awakeTask, shutdownTask))
                {
                    await shutdownTask;
                    acceptWebSocketTask.Discard();
                    return;
                }

                var webSocketContext = await acceptWebSocketTask;

                await this.PumpAsync(webSocketContext.WebSocket, shutdownTask).
                    ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                httpContext.Response.Close();

                if (Interlocked.Decrement(ref this.concurrentCount) <= 0)
                {
                    this.done!.SetResult(true);
                }
            }
        }

        private async void ListenAsynchronously()
        {
            Debug.Assert(this.httpListener != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);
            Debug.Assert(this.concurrentCount == 0);

            Interlocked.Increment(ref this.concurrentCount);

            try
            {
                var getContextTask = this.httpListener!.GetContextAsync();
                var shutdownTask = this.shutdown!.Task;

                while (true)
                {
                    var awakeTask = await Task.WhenAny(getContextTask, shutdownTask).
                        ConfigureAwait(false);
                    if (object.ReferenceEquals(awakeTask, shutdownTask))
                    {
                        await shutdownTask;
                        getContextTask.Discard();
                        break;
                    }

                    var httpContext = await getContextTask;

                    this.ConnectAsynchronously(httpContext);

                    getContextTask = this.httpListener!.GetContextAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                if (Interlocked.Decrement(ref this.concurrentCount) <= 0)
                {
                    this.done!.SetResult(true);
                }
            }
        }

        /// <summary>
        /// Start WebSocket server concurrently.
        /// </summary>
        /// <param name="serverPort">WebSocket server port</param>
        /// <param name="requiredSecureConnection">Will accept when secure connection is enabled</param>
        public void Start(int serverPort, bool requiredSecureConnection) =>
            this.Start($"{(requiredSecureConnection ? "https" : "http")}://+:{serverPort}/");

        /// <summary>
        /// Start WebSocket server concurrently.
        /// </summary>
        /// <param name="endPointUrl">WebSocket server endpoint</param>
        public void Start(string endPointUrl)
        {
            Debug.Assert(this.httpListener == null);
            Debug.Assert(this.shutdown == null);
            Debug.Assert(this.done == null);

            this.shutdown = new();
            this.done = new();
            this.httpListener = new();
            this.httpListener.Prefixes.Add(endPointUrl);
            this.httpListener.Start();

            this.ListenAsynchronously();
        }

        /// <summary>
        /// Calling when shutdown sequence.
        /// </summary>
        protected override async ValueTask OnShutdownAsync()
        {
            Debug.Assert(this.httpListener != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);

            this.shutdown!.SetResult(true);

            await this.done!.Task.
                ConfigureAwait(false);

            this.httpListener!.Close();
            this.httpListener = null;
            this.shutdown = null;
            this.done = null;
        }

        /// <summary>
        /// Calling when get sender stream.
        /// </summary>
        /// <returns>Stream</returns>
        protected override ValueTask<Stream> OnGetSenderStreamAsync()
        {
            if (this.httpListener is { })
            {
                // TODO: Broadcast all connections.
                WebSocketController[] controllers;
                lock (this.connections)
                {
                    controllers = this.connections.Values.
                        ToArray();
                }

                if (controllers.Length >= 1)
                {
                    var bridges = controllers.
                        Select(controller => controller.AllocateSenderStreamBridge()).
                        ToArray();
                    return new ValueTask<Stream>(new SenderStream(bridges));
                }
            }

            throw new ObjectDisposedException("WebSocketServerTransport already shutdowned.");
        }

        /// <summary>
        /// Create WebSocket transport instance.
        /// </summary>
        /// <returns>WebSocketServerTransport</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketServerTransport Create() =>
            new WebSocketServerTransport();
    }
}
