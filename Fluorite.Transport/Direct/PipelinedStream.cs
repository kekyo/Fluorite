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
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Direct
{
    internal struct PipelinedStreamPair
    {
        public readonly Stream ToWrite;
        public readonly Stream FromRead;

        internal PipelinedStreamPair(Stream toWrite, Stream fromRead)
        {
            this.ToWrite = toWrite;
            this.FromRead = fromRead;
        }
    }

    internal static class PipelinedStream
    {
        private sealed class WriterStream : Stream
        {
            private StreamBridge? bridge;

            public WriterStream(StreamBridge bridge) =>
                this.bridge = bridge;

            public override async Task FlushAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                if (this.bridge != null)
                {
                    await this.bridge.EnqueueFinishedAsync(token).
                        ConfigureAwait(false);
                    this.bridge = null;
                }
            }

            public override void Flush()
            {
                if (this.bridge != null)
                {
                    this.FlushAsync(default).
                        ConfigureAwait(false).
                        GetAwaiter().
                        GetResult();
                }
            }

            public override bool CanWrite =>
                true;

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.Flush();
                }
                base.Dispose(disposing);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                this.Write(buffer, offset, count);
#if NET45
                return Task.FromResult(true);
#else
                return Task.CompletedTask;
#endif
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (this.bridge != null)
                {
                    var streamData = new StreamData(buffer, offset, count);
                    this.bridge!.Enqueue(streamData);
                }
            }

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

        private sealed class ReaderStream : Stream
        {
            private readonly StreamBridge bridge;

            public ReaderStream(StreamBridge bridge) =>
                this.bridge = bridge;

            public override void Flush()
            {
            }

            public override bool CanRead =>
                true;

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.bridge.SetCompleted();
                }
                base.Dispose(disposing);
            }

            public override async Task<int> ReadAsync(
                byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var position = offset;
                var remains = count;

                while (remains >= 1)
                {
                    var streamData = await this.bridge.PeekAsync(cancellationToken).
                        ConfigureAwait(false);
                    if (streamData == null)
                    {
                        this.bridge.TryDequeue(out var _);
                        break;
                    }

                    var data = streamData.GetData();
                    var csize = Math.Min(remains, data.Count);
                    Buffer.BlockCopy(data.Array!, data.Offset, buffer, position, csize);

                    position += csize;
                    remains -= csize;

                    streamData.Forward(csize);
                    if (data.Count <= csize)
                    {
                        this.bridge.TryDequeue(out var _);
                    }
                }

                return count - remains;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                this.ReadAsync(buffer, offset, count, default).
                ConfigureAwait(false).
                GetAwaiter().
                GetResult();

#region Unused
            public override bool CanSeek =>
                false;

            public override bool CanWrite =>
                false;

            public override long Length =>
                throw new NotImplementedException();

            public override long Position
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new InvalidOperationException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotImplementedException();

            public override void SetLength(long value) =>
                throw new NotImplementedException();
#endregion
        }

        public static PipelinedStreamPair Create()
        {
            var bridge = new StreamBridge();

            var toWrite = new WriterStream(bridge);
            var fromRead = new ReaderStream(bridge);

            return new PipelinedStreamPair(toWrite, fromRead);
        }
    }
}
