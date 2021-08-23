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

using Fluorite.Internal;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    internal sealed class WebSocketController : IDisposable
    {
        private struct SendData
        {
            public readonly ArraySegment<byte> Data;
            public readonly TaskCompletionSource<bool> Completion;

            public SendData(ArraySegment<byte> data)
            {
                this.Data = data;
                this.Completion = new();
            }
        }

        private sealed class SendQueue : IDisposable
        {
            private readonly Queue<SendData> queue = new();
            private readonly AsyncManualResetEvent available = new();

            public void Dispose()
            {
                lock (this.queue)
                {
                    while (this.queue.Count >= 1)
                    {
                        this.queue.Dequeue().Completion.TrySetCanceled();
                    }
                    this.available.Reset();
                }
            }

            public Task SendAsync(ArraySegment<byte> data)
            {
                var sendData = new SendData(data);
                lock (this.queue)
                {
                    this.queue.Enqueue(sendData);
                    if (this.queue.Count == 1)
                    {
                        this.available.Set();
                    }
                }
                return sendData.Completion.Task;
            }

            public async Task<SendData> DequeueAsync(CancellationToken token)
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

        public Task SendAsync(ArraySegment<byte> data) =>
            this.sendQueue.SendAsync(data);

        public async Task RunAsync(Action<ArraySegment<byte>> action, Task shutdownTask)
        {
            var cts = new CancellationTokenSource();
            var buffer = new ExpandableBuffer(this.bufferElementSize);

            var receiveTask = this.webSocket.ReceiveAsync(buffer, cts.Token);
            var sendTask = sendQueue.DequeueAsync(cts.Token);

            try
            {
                while (true)
                {
                    var awakeTask = await Task.WhenAny(shutdownTask, receiveTask, sendTask).
                        ConfigureAwait(false);
                    if (object.ReferenceEquals(awakeTask, shutdownTask))
                    {
                        await shutdownTask.   // Make completion
                            ConfigureAwait(false);
                        var _ = receiveTask.ContinueWith(_ => { });    // ignoring sink
                        var __ = sendTask.ContinueWith(_ => { });      // ignoring sink
                        break;
                    }
                    else if (object.ReferenceEquals(awakeTask, sendTask))
                    {
                        var sendData = await sendTask.
                            ConfigureAwait(false);
                        try
                        {
                            await this.webSocket.SendAsync(sendData.Data, this.messageType, true, default).
                                ConfigureAwait(false);
                            sendData.Completion.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            sendData.Completion.TrySetException(ex);
                        }

                        sendTask = sendQueue.DequeueAsync(cts.Token);
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
                                action(buffer.Extract());
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