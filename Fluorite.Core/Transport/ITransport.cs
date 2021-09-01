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
using System.IO;
using System.Threading.Tasks;

namespace Fluorite.Transport
{
    /// <summary>
    /// Common interface for Fluorite transport.
    /// </summary>
    public interface ITransport
    {
        /// <summary>
        /// Initialize transport.
        /// </summary>
        /// <param name="receiver">Receiver calling when transport receive raw data</param>
        void Initialize(Func<Stream, ValueTask> receiver);

        /// <summary>
        /// Shutdown transport.
        /// </summary>
        ValueTask ShutdownAsync();

        /// <summary>
        /// Set transport payload content type.
        /// </summary>
        /// <param name="contentType">HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</param>
        void SetPayloadContentType(string contentType);

        /// <summary>
        /// Send peer with raw data.
        /// </summary>
        /// <param name="data">Raw data</param>
        ValueTask SendAsync(ArraySegment<byte> data);
    }
}
