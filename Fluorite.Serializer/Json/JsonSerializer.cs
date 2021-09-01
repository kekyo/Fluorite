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
using System.Text;
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

        public ValueTask<ArraySegment<byte>> SerializeAsync(Guid requestIdentity, string methodIdentity, object payload)
        {
            var container = new JsonContainer(requestIdentity, methodIdentity, payload);
            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(container);
            var data = Encoding.UTF8.GetBytes(jsonString);
            return new ValueTask<ArraySegment<byte>>(new ArraySegment<byte>(data));
        }

        public ValueTask<IPayloadContainerView> DeserializeAsync(ArraySegment<byte> data)
        {
            var jsonString = Encoding.UTF8.GetString(data.Array!, data.Offset, data.Count);
            var container = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonContainer>(jsonString);
            return new ValueTask<IPayloadContainerView>(container);
        }

        public static readonly JsonSerializer Instance =
            new JsonSerializer();
    }
}
