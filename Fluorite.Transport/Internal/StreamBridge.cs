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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class StreamBridge : IDisposable
    {
        private readonly AsyncQueue<StreamData?> queue = new();
        private readonly TaskCompletionSource<bool> completion = new();

        public void Dispose()
        {
            this.completion.TrySetCanceled();
            this.queue.Clear();
        }

        public void Enqueue(StreamData streamData) =>
            this.queue.Enqueue(streamData);

        public async Task EnqueueFinishedAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => this.completion.TrySetCanceled()))
            {
                this.queue.Enqueue(null);
                await this.completion.Task.
                    ConfigureAwait(false);
            }
        }

        public ValueTask<StreamData?> PeekAsync(CancellationToken token) =>
            this.queue.PeekAsync(token);

        public ValueTask<StreamData?> DequeueAsync(CancellationToken token) =>
            this.queue.DequeueAsync(token);

        public bool TryDequeue(out StreamData? streamData) =>
            this.queue.TryDequeue(out streamData);

        public void SetCompleted() =>
            this.completion.TrySetResult(true);

        public void SetFailed(Exception ex) =>
            this.completion.TrySetException(ex);
    }
}
