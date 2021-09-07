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
    internal sealed class JsonContainer : PayloadContainerBase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonContainer()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonContainer(Guid requestIdentity, string methodIdentity, object body) :
            base(requestIdentity, methodIdentity) =>
            this.Body = body;

        public object? Body { get; set; }

        public override int BodyCount =>
            this.Body is Newtonsoft.Json.Linq.JArray array ?
                array.Count :
                1;

        public override ValueTask<object?> DeserializeBodyAsync(int bodyIndex, Type type) =>
            new ValueTask<object?>(
                this.Body switch
                {
                    Newtonsoft.Json.Linq.JArray array => array[bodyIndex].ToObject(type)!,
                    Newtonsoft.Json.Linq.JToken token => token.ToObject(type),
                    _ => null!
                });
    }
}
