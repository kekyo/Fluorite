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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    internal sealed class WebSocketController : IDisposable
    {
        private sealed class SendQueue : IDisposable
        {
            private readonly Queue<ArraySegment<byte>> queue = new();
            private readonly AsyncManualResetEvent available = new();

            public void Dispose()
            {
                lock (this.queue)
                {
                    this.queue.Clear();
                    this.available.Reset();
                }
            }

            public void Enqueue(ArraySegment<byte> data)
            {
                lock (this.queue)
                {
                    this.queue.Enqueue(data);
                    if (this.queue.Count == 1)
                    {
                        this.available.Set();
                    }
                }
            }

            public async Task<ArraySegment<byte>> DequeueAsync(CancellationToken token)
            {
                while (true)
                {
                    await this.available.WaitAsync(token).
                        ConfigureAwait(false);

                    lock (this.queue)
                    {
                        if (this.queue.Count >= 1)
                        {
                            return this.queue.Dequeue();
                        }
                        this.available.Reset();
                    }
                }
            }
        }

        private readonly WebSocket webSocket;
        private readonly SendQueue sendQueue = new();
        private readonly WebSocketMessageType messageType;
        private readonly int bufferElementSize;

        public WebSocketController(
            WebSocket webSocket,
            WebSocketMessageType messageType,
            int bufferElementSize)
        {
            this.webSocket = webSocket;
            this.messageType = messageType;
            this.bufferElementSize = bufferElementSize;
        }

        public void Dispose() =>
            this.sendQueue.Dispose();

        public void SendAsynchronously(ArraySegment<byte> data) =>
            // Important: WebSocket couldn't multiple sending/receiving request at overlapped.
            //   So will logical deadlock when requests multiple with awaiting.
            //   WebSocket transport ignores awaiter, makes always fire-and-forget.
            this.sendQueue.Enqueue(data);

        public async Task RunAsync(Func<Stream, ValueTask> action, Task shutdownTask)
        {
            var cts = new CancellationTokenSource();
            var buffer = new ExpandableBuffer(this.bufferElementSize);

            var receiveTask = this.webSocket.ReceiveAsync(buffer, cts.Token);
            var sendTask = sendQueue.DequeueAsync(cts.Token);

            try
            {
                while (true)
                {
                    var awakeTask = await Task.WhenAny(shutdownTask, receiveTask, sendTask);
                    if (object.ReferenceEquals(awakeTask, shutdownTask))
                    {
                        await shutdownTask;   // Make completion
                        var _ = receiveTask.ContinueWith(_ => { });    // ignoring sink
                        var __ = sendTask.ContinueWith(_ => { });      // ignoring sink
                        break;
                    }
                    else if (object.ReferenceEquals(awakeTask, sendTask))
                    {
                        var data = await sendTask;
                        await this.webSocket.SendAsync(data, this.messageType, true, default);

                        sendTask = sendQueue.DequeueAsync(cts.Token);
                    }
                    else
                    {
                        var result = await receiveTask;
                        if (result.MessageType == this.messageType)
                        {
                            buffer.Adjust(result.Count);
                            if (result.EndOfMessage)
                            {
                                var data = buffer.Extract();
                                await action(new MemoryStream(data.Array!, data.Offset, data.Count));
                                buffer = new ExpandableBuffer(this.bufferElementSize);
                            }
                            else
                            {
                                buffer.Next();
                            }
                        }

                        if (result.CloseStatus != default)
                        {
                            var _ = sendTask.ContinueWith(_ => { });        // ignoring sink
                            var __ = shutdownTask.ContinueWith(_ => { });   // ignoring sink
                            break;
                        }

                        receiveTask = this.webSocket.ReceiveAsync(buffer, cts.Token);
                    }
                }
            }
            finally
            {
                cts.Cancel();
            }

            try
            {
                await this.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default).
                    ConfigureAwait(false);
            }
            catch
            {
                // Ignore.
            }
        }
    }
}
