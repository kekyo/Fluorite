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
using System.Linq;

namespace Fluorite
{
    /// <summary>
    /// Contains exception information at the peer.
    /// </summary>
#if !NETSTANDARD1_3
    [Serializable]
#endif
    public sealed class PeerException : AggregateException
    {
        /// <summary>
        /// Peer exception type.
        /// </summary>
        public readonly string PeerExceptionType;

        /// <summary>
        /// Peer stack trace if available.
        /// </summary>
        public readonly string? PeerStackTrace;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ei">Exception information</param>
        public PeerException(IExceptionInformationView ei) :
            base(ei.Message, ei.InnerExceptions.Select(iei => new PeerException(iei)).ToArray())
        {
            this.PeerExceptionType = ei.Type;
            this.PeerStackTrace = ei.StackTrace;
        }
    }
}
