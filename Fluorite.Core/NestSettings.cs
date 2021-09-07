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
using Fluorite.Transport;
using System.Runtime.CompilerServices;

namespace Fluorite
{
    /// <summary>
    /// Fluorite nest setting class.
    /// </summary>
    public sealed class NestSettings
    {
        /// <summary>
        /// The serializer will be used by Fluorite.
        /// </summary>
        public readonly ISerializer Serializer;

        /// <summary>
        /// The transport will be used by Fluorite.
        /// </summary>
        public readonly ITransport Transport;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="transport">Transport instance</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NestSettings(ISerializer serializer, ITransport transport)
        {
            this.Serializer = serializer;
            this.Transport = transport;
        }

        /// <summary>
        /// Create Fluorite nest setting instance.
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="transport">Transport instance</param>
        /// <returns>NestSettings</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NestSettings Create(ISerializer serializer, ITransport transport) =>
            new NestSettings(serializer, transport);
    }
}
