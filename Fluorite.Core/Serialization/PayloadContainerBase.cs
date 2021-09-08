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

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite.Serialization
{
    /// <summary>
    /// Multi purpose standard payload container base class.
    /// </summary>
    /// <remarks>Derives directly serialize/deserialize target when be applicable your serializer,
    /// or you have to implement custom payload container type with IPayloadContainerView from scratch.</remarks>
#if !NETSTANDARD1_3
    [Serializable]
#endif
    public abstract class PayloadContainerBase : IPayloadContainerView
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PayloadContainerBase()
        {
            this.RequestIdentity = default!;
            this.MethodIdentity = default!;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requestIdentity">Request identity</param>
        /// <param name="methodIdentity">Method identity</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PayloadContainerBase(Guid requestIdentity, string methodIdentity)
        {
            this.RequestIdentity = requestIdentity.ToString("N");
            this.MethodIdentity = methodIdentity;
        }

        /// <summary>
        /// Request identity.
        /// </summary>
        public string RequestIdentity { get; set; }

        /// <summary>
        /// Request identity.
        /// </summary>
        Guid IPayloadContainerView.RequestIdentity =>
            new Guid(this.RequestIdentity);

        /// <summary>
        /// Method identity.
        /// </summary>
        public string MethodIdentity { get; set; }

        /// <summary>
        /// Body data count.
        /// </summary>
        public abstract int BodyCount { get; }

        /// <summary>
        /// Deserialize a body data.
        /// </summary>
        /// <param name="index">Body index</param>
        /// <param name="type">Target type</param>
        /// <returns>Deserialized instance</returns>
        public abstract ValueTask<object?> DeserializeBodyAsync(int index, Type type);

        /// <summary>
        /// Get a string reflect this instance.
        /// </summary>
        /// <returns>String</returns>
        public override string ToString() =>
            $"{this.RequestIdentity}: {this.MethodIdentity}";
    }
}
