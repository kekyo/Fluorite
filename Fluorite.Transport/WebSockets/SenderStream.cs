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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    internal sealed class SenderStream : Stream
    {
        private readonly StreamBridge[] bridges;

        public SenderStream(StreamBridge[] bridges) =>
            this.bridges = bridges;

        public override void Flush()
        {
        }

        public override bool CanWrite =>
            true;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var bridge in this.bridges)
                {
                    // EOS
                    bridge.Finished();
                }
            }

            base.Dispose(disposing);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            Task.WhenAll(this.bridges.Select(bridge =>
            {
                var streamData = new StreamData(buffer, offset, count, token);
                bridge.Enqueue(streamData);
                return streamData.Task;
            }));

        public override void Write(byte[] buffer, int offset, int count) =>
            this.WriteAsync(buffer, offset, count, default).
            ConfigureAwait(false).
            GetAwaiter().
            GetResult();

        #region Unused
        public override bool CanSeek =>
            false;

        public override bool CanRead =>
            false;

        public override long Length =>
            throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotImplementedException();

        public override void SetLength(long value) =>
            throw new NotImplementedException();
        #endregion
    }
}
