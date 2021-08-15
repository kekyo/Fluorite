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

using Fluorite.Advanced;
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

        public ValueTask<ArraySegment<byte>> SerializeAsync(object body)
        {
            var js = new Newtonsoft.Json.JsonSerializer();

            var ms = new MemoryStream();
            var tw = new StreamWriter(ms);
            var jw = new Newtonsoft.Json.JsonTextWriter(tw);

            js.Serialize(jw, body);

            return new ValueTask<ArraySegment<byte>>(new ArraySegment<byte>(ms.ToArray()));
        }

        public ValueTask<TData> DeserializeAsync<TData>(ArraySegment<byte> data)
        {
            var js = new Newtonsoft.Json.JsonSerializer();

            var ms = new MemoryStream(data.Array!, 0, data.Count);
            var tr = new StreamReader(ms);
            var jr = new Newtonsoft.Json.JsonTextReader(tr);

            return new ValueTask<TData>(js.Deserialize<TData>(jr)!);
        }

        public static readonly JsonSerializer Instance =
            new JsonSerializer();
    }
}
