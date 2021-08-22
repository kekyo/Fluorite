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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class AsyncLock
    {
        private readonly Disposer disposer;
        private readonly Queue<TaskCompletionSource<IDisposable>> queue = new();
        private bool isRunning;

        public AsyncLock() =>
            this.disposer = new(this);

        public IDisposable Lock()
        {
            TaskCompletionSource<IDisposable> tcs;
            lock (this.queue)
            {
                if (!this.isRunning)
                {
                    Debug.Assert(this.queue.Count == 0);

                    this.isRunning = true;
                    return this.disposer;
                }

                tcs = new TaskCompletionSource<IDisposable>();
                this.queue.Enqueue(tcs);
            }

            return tcs.Task.Result;
        }

        public ValueTask<IDisposable> LockAsync()
        {
            lock (this.queue)
            {
                if (!this.isRunning)
                {
                    Debug.Assert(this.queue.Count == 0);

                    this.isRunning = true;
                    return new ValueTask<IDisposable>(this.disposer);
                }

                var tcs = new TaskCompletionSource<IDisposable>();
                this.queue.Enqueue(tcs);

                return new ValueTask<IDisposable>(tcs.Task);
            }
        }

        private void InternalDispose()
        {
            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    Debug.Assert(this.isRunning);

                    this.queue.Dequeue().SetResult(this.disposer);
                }

                if (this.queue.Count == 0)
                {
                    this.isRunning = false;
                }
            }
        }

        private sealed class Disposer : IDisposable
        {
            private readonly AsyncLock parent;

            public Disposer(AsyncLock parent) =>
                this.parent = parent;

            public void Dispose() =>
                this.parent.InternalDispose();
        }
    }
}
