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
    public abstract class TransportBase :
        ITransport
    {
        private Func<ArraySegment<byte>, ValueTask>? receiver;

        protected TransportBase()
        {
        }

        void ITransport.RegisterReceiver(Func<ArraySegment<byte>, ValueTask> receiver)
        {
            if (Interlocked.CompareExchange(ref this.receiver, receiver, null) != null)
            {
                throw new InvalidOperationException("Receiver already registered.");
            }
        }

        void ITransport.UnregisterReceiver(Func<ArraySegment<byte>, ValueTask> receiver)
        {
            if (Interlocked.CompareExchange(ref this.receiver, null, receiver) == null)
            {
                throw new InvalidOperationException("It isn't registered.");
            }
        }

        protected virtual void SetPayloadContentType(string contentType)
        {
        }

        void ITransport.SetPayloadContentType(string contentType) =>
            this.SetPayloadContentType(contentType);

        protected ValueTask OnReceivedAsync(ArraySegment<byte> data)
        {
            var receiver = this.receiver;
            if (receiver == null)
            {
                throw new InvalidOperationException("Receiver isn't registered.");
            }

            return receiver(data);
        }

        public abstract ValueTask SendAsync(ArraySegment<byte> data);

        public virtual ValueTask ShutdownAsync() =>
            default;
    }
}
