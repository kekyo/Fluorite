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
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class AsyncLock
    {
        private readonly Queue<TaskCompletionSource<bool>> queue = new();

        public ValueTask<IDisposable> LockAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            lock (this.queue)
            {
                this.queue.Enqueue(tcs);
            }
            return new ValueTask<IDisposable>(tcs.Task);
        }

        private void InternalDispose()
        {
            lock (this.queue)
            {
                if (this.queue.Count >= 1)
                {
                    this.queue.Dequeue().SetResult(true);
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
