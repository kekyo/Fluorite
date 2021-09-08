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

namespace Fluorite.Serialization
{
    /// <summary>
    /// Fluorite serializer interface.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Payload content type string for this serializer content.
        /// </summary>
        /// <remarks>HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</remarks>
        string PayloadContentType { get; }

        /// <summary>
        /// Perform serialize with header values and body.
        /// </summary>
        /// <param name="writeTo">Serialize into this stream</param>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        /// <param name="body">Body data</param>
        ValueTask SerializeAsync(Stream writeTo, Guid requestIdentity, string methodIdentity, object? body);

        /// <summary>
        /// Perform serialize with exception information.
        /// </summary>
        /// <param name="writeTo">Serialize into this stream</param>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        /// <param name="ei">Exception information</param>
        ValueTask SerializeExceptionAsync(Stream writeTo, Guid requestIdentity, string methodIdentity, ExceptionInformation ei);

        /// <summary>
        /// Perform deserialize from a stream.
        /// </summary>
        /// <param name="readFrom">Deserialize from this stream</param>
        /// <returns>Payload container view instance</returns>
        ValueTask<IPayloadContainerView> DeserializeAsync(Stream readFrom);
    }
}
