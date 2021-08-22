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
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class AsyncManualResetEvent
    {
        private readonly List<TaskCompletionSource<bool>> awaitings = new();
        private volatile bool signaled;

        public void Set()
        {
            TaskCompletionSource<bool>[] awaitings;
            lock (this.awaitings)
            {
                this.signaled = true;

                awaitings = this.awaitings.ToArray();
                this.awaitings.Clear();
            }

            foreach (var awaiting in awaitings)
            {
                awaiting.TrySetResult(true);
            }
        }

        public void Reset() =>
            this.signaled = false;

        public ValueTask WaitAsync()
        {
            lock (this.awaitings)
            {
                if (this.signaled)
                {
                    return default;
                }

                var tcs = new TaskCompletionSource<bool>();
                this.awaitings.Add(tcs);

                return new ValueTask(tcs.Task);
            }
        }
    }
}
