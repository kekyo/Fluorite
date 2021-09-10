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
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    internal sealed class WebSocketController : IDisposable
    {
        private readonly WebSocket webSocket;
        private readonly WebSocketMessageType messageType;
        private readonly int bufferElementSize;
        private readonly AsyncQueue<StreamBridge> bridgeQueue = new();

        public WebSocketController(
            WebSocket webSocket,
            WebSocketMessageType messageType,
            int bufferElementSize)
        {
            this.webSocket = webSocket;
            this.messageType = messageType;
            this.bufferElementSize = bufferElementSize;
        }

        public void Dispose()
        {
            while (this.bridgeQueue.TryDequeue(out var bridge))
            {
                bridge.Dispose();
            }
        }

        /// <summary>
        /// Get WebSocket message type from content type string.
        /// </summary>
        /// <param name="contentType">HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</param>
        public static WebSocketMessageType GetMessageType(string contentType) =>
            (contentType == "application/octet-stream") ?
                WebSocketMessageType.Binary :
                WebSocketMessageType.Text;

        public StreamBridge AllocateSenderStreamBridge()
        {
            var bridge = new StreamBridge();
            this.bridgeQueue.Enqueue(bridge);
            return bridge;
        }

        private async Task RunSenderAsync(
            Task shutdownTask, TaskCompletionSource<bool> exitRequest, CancellationToken token)
        {
            var bridgeTask = this.bridgeQueue.DequeueAsync(token).
                AsTask();

            while (true)
            {
                var awakeTask = await Task.WhenAny(shutdownTask, exitRequest.Task, bridgeTask).
                    ConfigureAwait(false);
                if (object.ReferenceEquals(awakeTask, shutdownTask))
                {
                    await shutdownTask;   // Make completion

                    bridgeTask.Discard();
                    exitRequest.SetResult(true);   // Tell receiver.
                    return;
                }
                if (object.ReferenceEquals(awakeTask, exitRequest.Task))
                {
                    await exitRequest.Task;   // Make completion

                    bridgeTask.Discard();
                    return;
                }

                Debug.Assert(object.ReferenceEquals(awakeTask, bridgeTask));

                var bridge = await bridgeTask;
                var sendTask = bridge.DequeueAsync(token).
                    AsTask();

                while (true)
                {
                    awakeTask = await Task.WhenAny(shutdownTask, exitRequest.Task, sendTask).
                        ConfigureAwait(false);
                    if (object.ReferenceEquals(awakeTask, shutdownTask))
                    {
                        await shutdownTask;   // Make completion

                        sendTask.Discard();
                        exitRequest.TrySetResult(true);   // Tell receiver.
                        return;
                    }
                    if (object.ReferenceEquals(awakeTask, exitRequest.Task))
                    {
                        await exitRequest.Task;   // Make completion

                        sendTask.Discard();
                        return;
                    }

                    var streamData = await sendTask;
                    if (streamData == null)
                    {
                        // EOS
                        await this.webSocket.SendAsync(
                            new ArraySegment<byte>(new byte[0], 0, 0),
                            this.messageType,
                            true,
                            token).
                            ConfigureAwait(false);
                        break;
                    }

                    try
                    {
                        await this.webSocket.SendAsync(
                            streamData.GetData(),
                            this.messageType,
                            false,
                            token).
                            ConfigureAwait(false);
                        streamData.SetCompleted();
                    }
                    catch (Exception ex)
                    {
                        streamData.SetException(ex);
                    }

                    sendTask = bridge.DequeueAsync(token).
                        AsTask();
                }

                bridgeTask = this.bridgeQueue.DequeueAsync(token).
                    AsTask();
            }
        }

        private async Task RunReceiverAsync(
            Func<Stream, ValueTask> receivedAction,
            Task shutdownTask,
            TaskCompletionSource<bool> exitRequest,
            CancellationToken token)
        {
            var buffer = new ExpandableBufferStream(this.bufferElementSize);

            var receiveTask = this.webSocket.ReceiveAsync(buffer.GetPartialBuffer(), token);

            while (true)
            {
                var awakeTask = await Task.WhenAny(shutdownTask, exitRequest.Task, receiveTask).
                    ConfigureAwait(false);
                if (object.ReferenceEquals(awakeTask, shutdownTask))
                {
                    await shutdownTask;   // Make completion

                    receiveTask.Discard();
                    exitRequest.TrySetResult(true);   // Tell sender.
                    return;
                }
                if (object.ReferenceEquals(awakeTask, exitRequest.Task))
                {
                    await exitRequest.Task;   // Make completion

                    receiveTask.Discard();
                    return;
                }

                Debug.Assert(object.ReferenceEquals(awakeTask, receiveTask));

                var result = await receiveTask;
                if (result.MessageType == this.messageType)
                {
                    buffer.CommitPartialBuffer(result.Count);

                    if (result.EndOfMessage)
                    {
                        buffer.ReadyToRead();

                        await receivedAction(buffer);

                        buffer = new ExpandableBufferStream(this.bufferElementSize);
                    }
                }

                if (result.CloseStatus != default)
                {
                    shutdownTask.Discard();
                    exitRequest.TrySetResult(true);   // Tell receiver.
                    return;
                }

                receiveTask = this.webSocket.ReceiveAsync(buffer.GetPartialBuffer(), token);
            }
        }

        public async ValueTask RunAsync(
            Func<Stream, ValueTask> receivedAction,
            Task shutdownTask)
        {
            var cts = new CancellationTokenSource();

            try
            {
                var exitRequest = new TaskCompletionSource<bool>();

                await Task.WhenAll(
                    this.RunSenderAsync(shutdownTask, exitRequest, cts.Token),
                    this.RunReceiverAsync(receivedAction, shutdownTask, exitRequest, cts.Token)).
                    ConfigureAwait(false);

                try
                {
                    // Feather shutdown.
                    await this.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default).
                        ConfigureAwait(false);
                }
                catch
                {
                    // Ignore.
                }
            }
            finally
            {
                // Force discards non feather shutdowned resources.
                // (Will sink into Task.Discard)
                cts.Cancel();
            }
        }
    }
}
