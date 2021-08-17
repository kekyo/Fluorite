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

namespace Fluorite.Internal
{
    internal sealed class ExpandableBuffer
    {
        private readonly int elementSize;
        private byte[] buffer;
        private int begin;
        private int size;

        public ExpandableBuffer(int elementSize)
        {
            this.elementSize = elementSize;
            this.buffer = new byte[this.elementSize];
            this.size = this.elementSize;
        }

        public int Size =>
            this.size;

        public void Adjust(int size)
        {
            Debug.Assert((this.begin + size) < this.buffer.Length);
            this.size = size;
        }

        public void Next()
        {
            this.begin += this.size;
            this.size = this.buffer.Length - this.begin;
            if (this.size <= 0)
            {
                var newBuffer = new byte[this.buffer.Length + this.elementSize];
                Array.Copy(this.buffer, newBuffer, this.buffer.Length);

                this.buffer = newBuffer;
                this.size = this.elementSize;
            }
        }

        public ArraySegment<byte> Extract() =>
            new ArraySegment<byte>(this.buffer, 0, this.begin + this.size);

        public static implicit operator ArraySegment<byte>(ExpandableBuffer buffer) =>
            new ArraySegment<byte>(buffer.buffer, buffer.begin, buffer.size);
    }
}
