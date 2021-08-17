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

namespace Fluorite.Transport
{
    public struct DirectAttachedTransportPair
    {
        public readonly ITransport Transport1;
        public readonly ITransport Transport2;

        internal DirectAttachedTransportPair(ITransport transport1, ITransport transport2)
        {
            this.Transport1 = transport1;
            this.Transport2 = transport2;
        }
    }

    public sealed class DirectAttachedTransport : TransportBase
    {
        private DirectAttachedTransport? peer;

        private DirectAttachedTransport()
        {
        }

        public override ValueTask SendAsync(ArraySegment<byte> data)
        {
            // Fire and forgot each request, because the request can send with no waiting on inter process.
#if NETSTANDARD1_3
            Task.Run(() => this.peer!.OnReceived(data));
#else
            ThreadPool.QueueUserWorkItem(_ => this.peer!.OnReceived(data));
#endif
            return default;
        }

        public static DirectAttachedTransportPair Create()
        {
            var transport1 = new DirectAttachedTransport();
            var transport2 = new DirectAttachedTransport();

            transport1.peer = transport2;
            transport2.peer = transport1;

            return new DirectAttachedTransportPair(transport1, transport2);
        }
    }
}
