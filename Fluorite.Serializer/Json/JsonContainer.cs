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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite.Json
{
    /// <summary>
    /// Payload container for Json serializer.
    /// </summary>
    internal sealed class JsonContainer : PayloadContainerBase
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonContainer()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        /// <param name="body">Payload body data</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonContainer(Guid requestIdentity, string methodIdentity, object? body) :
            base(requestIdentity, methodIdentity) =>
            this.Body = (body != null) ? Newtonsoft.Json.Linq.JToken.FromObject(body) : null;

        /// <summary>
        /// Payload body data.
        /// </summary>
        /// <remarks>It will contain an array when data is arguments.</remarks>
        public Newtonsoft.Json.Linq.JToken? Body { get; set; }

        /// <summary>
        /// Number of payload body data.
        /// </summary>
        public override int BodyCount =>
            this.Body is Newtonsoft.Json.Linq.JArray array ?
                array.Count :
                1;

        /// <summary>
        /// Deserialize a body data.
        /// </summary>
        /// <param name="bodyIndex">Body index</param>
        /// <param name="type">Target type</param>
        /// <returns>Deserialized instance</returns>
        public override ValueTask<object?> DeserializeBodyAsync(int bodyIndex, Type type) =>
            new ValueTask<object?>(
                // Newtonsoft.Json can deserialize directly ExceptionInformation class,
                // so we don't do any specialization at here.
                this.Body switch
                {
                    Newtonsoft.Json.Linq.JArray array => array[bodyIndex].ToObject(type)!,
                    Newtonsoft.Json.Linq.JToken token => token.ToObject(type),
                    _ => null
                });
    }
}
