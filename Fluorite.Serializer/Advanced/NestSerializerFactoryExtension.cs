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

using Fluorite.Json;
using Fluorite.Proxy;
using Fluorite.Transport;

namespace Fluorite.Advanced
{
    public static class NestSerializerFactoryExtension
    {
        public static Nest Create(
            this NestFactory _, ITransport transport, IPeerProxyFactory factory) =>
            _.Create(NestSettings.Create(JsonSerializer.Instance, transport), factory);
    }
}
