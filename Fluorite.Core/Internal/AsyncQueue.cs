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

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class AsyncQueue<T>
    {
        private readonly Queue<T> queue = new();
        private readonly AsyncManualResetEvent available = new();

        public void Enqueue(T value)
        {
            lock (this.queue)
            {
                this.queue.Enqueue(value);
                if (this.queue.Count == 1)
                {
                    this.available.Set();
                }
            }
        }

        public async ValueTask<T> PeekAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    return this.queue.Peek();
                }

                this.available.Reset();
            }

            while (true)
            {
                await this.available.WaitAsync(token).
                    ConfigureAwait(false);

                lock (this.queue)
                {
                    if (this.queue.Count >= 1)
                    {
                        return this.queue.Peek();
                    }

                    this.available.Reset();
                }
            }
        }

        public bool TryPeek(out T value)
        {
            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    value = this.queue.Peek();
                    return true;
                }
                else
                {
                    this.available.Reset();
                    value = default!;
                    return false;
                }
            }
        }

        public async ValueTask<T> DequeueAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    return this.queue.Dequeue();
                }

                this.available.Reset();
            }

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

        public bool TryDequeue(out T value)
        {
            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    value = this.queue.Dequeue();
                    return true;
                }
                else
                {
                    this.available.Reset();
                    value = default!;
                    return false;
                }
            }
        }
    }
}
