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
    public sealed class JsonSerializer : ISerializer
    {
        private JsonSerializer()
        {
        }

        public string PayloadContentType =>
            "application/json";

        public async ValueTask SerializeAsync(Stream writeTo, Guid requestIdentity, string methodIdentity, object body)
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

        public async ValueTask<IPayloadContainerView> DeserializeAsync(Stream readFrom)
        {
            var tr = new StreamReader(readFrom);
            var jr = new Newtonsoft.Json.JsonTextReader(tr);

            var jtoken = await Newtonsoft.Json.Linq.JToken.ReadFromAsync(jr).
                ConfigureAwait(false);
            return jtoken.ToObject<JsonContainer>()!;
        }

        public static readonly JsonSerializer Instance =
            new JsonSerializer();
    }
}
