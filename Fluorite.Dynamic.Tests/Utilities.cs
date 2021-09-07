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

using Fluorite.Direct;
using Fluorite.Json;
using Fluorite.Serialization;

namespace Fluorite
{
    public static class Utilities
    {
        public static (Nest server, Nest client) CreateDirectAttachedNestPair(ISerializer serializer)
        {
            // Dumb transport set are marshaling only sandwiches serializer.
            var transports = DirectAttachedTransport.Create();

            var server = Nest.Factory.Create(
                NestSettings.Create(serializer, transports.Transport1));
            var client = Nest.Factory.Create(
                NestSettings.Create(serializer, transports.Transport2));

            return (server, client);
        }

        public static (Nest server, Nest client) CreateDirectAttachedNestPair() =>
            CreateDirectAttachedNestPair(JsonSerializer.Instance);
    }
}
