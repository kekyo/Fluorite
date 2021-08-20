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
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    public sealed class WebSocketServerTransport :
        TransportBase
    {
        private const int BufferElementSize = 16384;

        private readonly Dictionary<WebSocket, string> connections = new();
        private readonly AsyncLock sendLocker = new();

        private HttpListener? httpListener;
        private TaskCompletionSource<bool>? shutdown;
        private TaskCompletionSource<bool>? done;
        private volatile int concurrentCount;
        private WebSocketMessageType messageType = WebSocketMessageType.Text;

        private WebSocketServerTransport()
        {
        }

        private async ValueTask PumpAsync(string webSocketKey, WebSocket webSocket, Task shutdownTask)
        {
            Debug.Assert(this.httpListener != null);
            Debug.Assert(this.shutdown != null);
            Debug.Assert(this.done != null);

            lock (this.connections)
            {
                this.connections[webSocket] = webSocketKey;
            }

            try
            {
                var buffer = new ExpandableBuffer(BufferElementSize);
                var receiveTask = webSocket.ReceiveAsync(buffer, default);

                while (true)
                {
                    var awakeTask = await Task.WhenAny(receiveTask, shutdownTask).
                        ConfigureAwait(false);
                    if (object.ReferenceEquals(awakeTask, shutdownTask))
                    {
                        await shutdownTask.
                            ConfigureAwait(false);
                        var _ = receiveTask.ContinueWith(_ => { });    // ignoring sink
                        break;
                    }
                    else
                    {
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
                            var __ = shutdownTask.ContinueWith(_ => { });    // ignoring sink
                            break;
                        }

                        receiveTask = webSocket.ReceiveAsync(buffer, default);
                    }
                }

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default).
                    ConfigureAwait(false);
            }
            finally
            {
                lock (this.connections)
                {
                    if (this.connections.TryGetValue(webSocket, out var current) &&
                        webSocketKey.Equals(current))
                    {
                        this.connections.Remove(webSocket);
                    }
                }

                webSocket.Dispose();
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
                    await shutdownTask.
                        ConfigureAwait(false);
                    var _ = acceptWebSocketTask.ContinueWith(_ => { });    // ignoring sink
                    return;
                }

                var webSocketContext = await acceptWebSocketTask.
                    ConfigureAwait(false);

                await this.PumpAsync(webSocketContext.SecWebSocketKey, webSocketContext.WebSocket, shutdownTask).
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
                        await shutdownTask.
                            ConfigureAwait(false);
                        var _ = getContextTask.ContinueWith(_ => { });    // ignoring sink
                        break;
                    }

                    var httpContext = await getContextTask.
                        ConfigureAwait(false);

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

        public void Start(int serverPort, bool requiredSecureConnection) =>
            this.Start($"{(requiredSecureConnection ? "https" : "http")}://+:{serverPort}/");

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

        public override async ValueTask ShutdownAsync()
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

        public override async ValueTask SendAsync(ArraySegment<byte> data)
        {
            using (await this.sendLocker.LockAsync().
                ConfigureAwait(false))
            {
                // TODO: awaiting each websockets.
                Task[] completions;
                lock (this.connections)
                {
                    completions = this.connections.Keys.
                        Select(webSocket => webSocket.SendAsync(data, this.messageType, true, default)).
                        ToArray();
                }

                await Task.WhenAll(completions).
                    ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketServerTransport Create() =>
            new WebSocketServerTransport();
    }
}
