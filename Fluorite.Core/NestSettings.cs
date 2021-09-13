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
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fluorite
{
    /// <summary>
    /// Fluorite nest setting class.
    /// </summary>
    public sealed class NestSettings
    {
        private readonly List<IHost> exposeObjects;

        /// <summary>
        /// The serializer will be used by Fluorite.
        /// </summary>
        public ISerializer Serializer { get; private set; }

        /// <summary>
        /// The transport will be used by Fluorite.
        /// </summary>
        public ITransport Transport { get; private set; }

        /// <summary>
        /// Perform contains stack trace from peer.
        /// </summary>
        public bool ContainsStackTrace { get; private set; }

        /// <summary>
        /// Expose object instances
        /// </summary>
        public IReadOnlyList<IHost> ExposeObjects =>
            this.exposeObjects;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="transport">Transport instance</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NestSettings(
            ISerializer serializer,
            ITransport transport)
        {
            this.Serializer = serializer;
            this.Transport = transport;
            this.exposeObjects = new();
        }

        /// <summary>
        /// Enable contains stack trace on payload container.
        /// </summary>
        /// <returns>NestSettings</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NestSettings EnableContainsStackTrace()
        {
            this.ContainsStackTrace = true;
            return this;
        }

        /// <summary>
        /// Add exposing object instances.
        /// </summary>
        /// <param name="exposeObjects">Expose object instances</param>
        /// <returns>NestSettings</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NestSettings AddExposeObjects(params IHost[] exposeObjects)
        {
            this.exposeObjects.AddRange(exposeObjects);
            return this;
        }

        /// <summary>
        /// Create Fluorite nest setting instance.
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="transport">Transport instance</param>
        /// <returns>NestSettings</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NestSettings Create(
            ISerializer serializer,
            ITransport transport) =>
            new NestSettings(serializer, transport);
    }
}
