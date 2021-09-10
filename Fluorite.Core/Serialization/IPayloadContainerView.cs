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
using System.Threading.Tasks;

#pragma warning disable CS1573

namespace Fluorite.Serialization
{
    /// <summary>
    /// Deserialized payload container view interface.
    /// </summary>
    public interface IPayloadContainerView
    {
        /// <summary>
        /// Request identity.
        /// </summary>
        Guid RequestIdentity { get; }

        /// <summary>
        /// Method identity.
        /// </summary>
        string MethodIdentity { get; }

        /// <summary>
        /// Body data count.
        /// </summary>
        int BodyCount { get; }

        /// <summary>
        /// Deserialize a body data.
        /// </summary>
        /// <param name="bodyIndex">Body index</param>
        /// <param name="type">Target type</param>
        /// <returns>Deserialized instance</returns>
        ValueTask<object?> DeserializeBodyAsync(int bodyIndex, Type type);

        /// <summary>
        /// Deserialize an exception.
        /// </summary>
        /// <returns>Deserialized exception</returns>
        /// <remarks>Mostly returns PeerException instance.</remarks>
        ValueTask<Exception> DeserializeExceptionAsync();
    }

    public static class SerializerExtension
    {
        /// <summary>
        /// Deserialize a body data.
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <param name="bodyIndex">Body index</param>
        /// <returns>Deserialized instance</returns>
        public static async ValueTask<T> DeserializeBodyAsync<T>(this IPayloadContainerView pcv, int bodyIndex) =>
            (T)(await pcv.DeserializeBodyAsync(bodyIndex, typeof(T)).ConfigureAwait(false))!;
    }
}
