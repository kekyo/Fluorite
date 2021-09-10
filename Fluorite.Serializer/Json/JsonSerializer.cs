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

using Fluorite.Serialization;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Fluorite.Json
{
    /// <summary>
    /// Fluorite defaulted Json serializer.
    /// </summary>
    public sealed class JsonSerializer : ISerializer
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        private JsonSerializer()
        {
        }

        /// <summary>
        /// Payload content type string for this serializer content.
        /// </summary>
        /// <remarks>HTTP content type like string ('application/json', 'application/octet-stream' and etc...)</remarks>
        public string PayloadContentType =>
            "application/json";

        /// <summary>
        /// Perform serialize with header values and body.
        /// </summary>
        /// <param name="writeTo">Serialize into this stream</param>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        /// <param name="body">Body data</param>
        public async ValueTask SerializeAsync(Stream writeTo, Guid requestIdentity, string methodIdentity, object? body)
        {
            var container = new JsonContainer(requestIdentity, methodIdentity, body);
            var jtoken = Newtonsoft.Json.Linq.JToken.FromObject(container);

            var tw = new StreamWriter(writeTo);   // Suppressed BOM UTF8
            var jw = new Newtonsoft.Json.JsonTextWriter(tw);

            await jtoken.WriteToAsync(jw).
                ConfigureAwait(false);
            await jw.FlushAsync().
                ConfigureAwait(false);
        }

        /// <summary>
        /// Perform serialize with an exception.
        /// </summary>
        /// <param name="writeTo">Serialize into this stream</param>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        /// <param name="ex">Target exception</param>
        public ValueTask SerializeExceptionAsync(Stream writeTo, Guid requestIdentity, string methodIdentity, Exception ex) =>
            // Newtonsoft.Json can serialize directly ExceptionInformation class.
            this.SerializeAsync(writeTo, requestIdentity, methodIdentity, new ExceptionInformation(ex));

        /// <summary>
        /// Perform deserialize from a stream.
        /// </summary>
        /// <param name="readFrom">Deserialize from this stream</param>
        /// <returns>Payload container view instance</returns>
        public async ValueTask<IPayloadContainerView> DeserializeAsync(Stream readFrom)
        {
            var tr = new StreamReader(readFrom);
            var jr = new Newtonsoft.Json.JsonTextReader(tr);

            var jtoken = await Newtonsoft.Json.Linq.JToken.ReadFromAsync(jr).
                ConfigureAwait(false);
            return jtoken.ToObject<JsonContainer>()!;
        }

        /// <summary>
        /// Json serializer instance.
        /// </summary>
        public static readonly JsonSerializer Instance =
            new JsonSerializer();
    }
}
