﻿////////////////////////////////////////////////////////////////////////////
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

namespace Fluorite.Transport
{
    /// <summary>
    /// Base transport implementation for useful features.
    /// </summary>
    public abstract class TransportBase :
        ITransport
    {
        private Func<ArraySegment<byte>, ValueTask>? receiver;

        /// <summary>
        /// Constructor.
        /// </summary>
        protected TransportBase()
        {
        }

        /// <summary>
        /// Initialize transport.
        /// </summary>
        /// <param name="receiver">Receiver calling when transport receive raw data</param>
        void ITransport.Initialize(Func<ArraySegment<byte>, ValueTask> receiver)
        {
            Debug.Assert(this.receiver == null);
            this.receiver = receiver;
        }

        /// <summary>
        /// Calling when shutdown sequence.
        /// </summary>
        protected virtual ValueTask OnShutdownAsync() =>
            default;

        /// <summary>
        /// Shutdown transport.
        /// </summary>
        ValueTask ITransport.ShutdownAsync()
        {
            if (Interlocked.CompareExchange(ref this.receiver, null, this.receiver) != null)
            {
                return this.OnShutdownAsync();
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Calling when set payload content type.
        /// </summary>
        /// <param name="contentType">HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</param>
        protected virtual void OnSetPayloadContentType(string contentType)
        {
        }

        /// <summary>
        /// Set transport payload content type.
        /// </summary>
        /// <param name="contentType">HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</param>
        void ITransport.SetPayloadContentType(string contentType) =>
            this.OnSetPayloadContentType(contentType);

        /// <summary>
        /// Transfer received raw data.
        /// </summary>
        /// <param name="data">Raw data</param>
        protected ValueTask OnReceivedAsync(ArraySegment<byte> data)
        {
            var receiver = this.receiver;
            if (receiver == null)
            {
                throw new InvalidOperationException("Transport isn't initialized.");
            }

            return receiver(data);
        }

        /// <summary>
        /// Calling when send peer with raw data.
        /// </summary>
        /// <param name="data">Raw data</param>
        protected abstract ValueTask OnSendAsync(ArraySegment<byte> data);

        /// <summary>
        /// Send peer with raw data.
        /// </summary>
        /// <param name="data">Raw data</param>
        ValueTask ITransport.SendAsync(ArraySegment<byte> data) =>
            this.OnSendAsync(data);
    }
}
