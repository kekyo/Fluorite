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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal sealed class ExpandableBufferStream :
        Stream
    {
        private readonly int bufferElementSize;
        private byte[] buffer;
        private int position;
        private int length;

        public ExpandableBufferStream(int bufferElementSize)
        {
            this.bufferElementSize = bufferElementSize;
            this.buffer = new byte[bufferElementSize];
        }

        public override bool CanRead =>
            true;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            this.length;

        public override long Position
        {
            get => this.position;
            set => throw new InvalidOperationException();
        }

        private void EnsureCapacity()
        {
            if (this.position > (this.buffer.Length * 2 / 3))
            {
                var newBuffer = new byte[this.buffer.Length + this.bufferElementSize];
                Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.buffer.Length);
                this.buffer = newBuffer;
            }
        }

        public ArraySegment<byte> GetPartialBuffer()
        {
            this.EnsureCapacity();
            return new ArraySegment<byte>(this.buffer, this.position, this.buffer.Length - this.position);
        }

        public void CommitPartialBuffer(int size)
        {
            Debug.Assert((this.position + size) <= this.buffer.Length);
            this.position += size;
        }

        public void ReadyToRead()
        {
            this.length = this.position;
            this.position = 0;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if ((offset + count) > buffer.Length)
            {
                throw new ArgumentException();
            }

            var rcount = Math.Min(count, this.length - this.position);
            Buffer.BlockCopy(this.buffer, this.position, buffer, offset, rcount);

            this.position += rcount;

            return rcount;
        }

        public override Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = this.Read(buffer, offset, count);
            return Task.FromResult(read);
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotImplementedException();

        public override void SetLength(long value) =>
            throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();
    }
}
