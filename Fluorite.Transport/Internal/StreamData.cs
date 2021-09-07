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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class StreamData : IDisposable
    {
        private ArraySegment<byte> data;
        private readonly TaskCompletionSource<bool> completion = new();
        private readonly IDisposable registration;

        public StreamData(byte[] data, int offset, int size, CancellationToken token)
        {
            this.data = new ArraySegment<byte>(data, offset, size);
            this.registration = token.Register(() => this.completion.TrySetCanceled());
        }

        public Task Task =>
            this.completion.Task;

        public ArraySegment<byte> GetData()
        {
            lock (this)
            {
                return this.data;
            }
        }

        public void Forward(int size)
        {
            lock (this)
            {
                Debug.Assert(this.data.Count >= size);
                this.data = new ArraySegment<byte>(this.data.Array!, this.data.Offset + size, this.data.Count - size);
            }
        }

        public void Dispose()
        {
            this.completion.TrySetCanceled();
            this.registration.Dispose();
        }

        public void SetCompleted()
        {
            this.completion.TrySetResult(true);
            this.registration.Dispose();
        }

        public void SetException(Exception ex)
        {
            this.completion.TrySetException(ex);
            this.registration.Dispose();
        }
    }
}
