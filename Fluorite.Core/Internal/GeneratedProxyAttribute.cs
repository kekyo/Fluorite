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
using System.ComponentModel;

namespace Fluorite.Internal
{
    /// <summary>
    /// A marker base class of injected static proxy definitions.
    /// </summary>
    /// <remarks>Will be applied this attribute by automated proxy generator. (Fluorite.Build)</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.Assembly)]   // HACK: refer AttributeUsage type from Fluorite.Build
    public abstract class GeneratedProxyAttribute : Attribute
    {
        private static volatile object locker = new object();
        private static volatile bool initialized;

        /// <summary>
        /// Constructor.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected GeneratedProxyAttribute()
        {
        }

        /// <summary>
        /// Initialize static proxy.
        /// </summary>
        /// <remarks>Will be overrided by the proxy generator.</remarks>
        protected abstract void OnInitialize();

        /// <summary>
        /// Initialize static proxy.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Initialize()
        {
            if (!initialized)
            {
                lock (locker)
                {
                    if (!initialized)
                    {
                        initialized = true;
                        locker = null!;
                        this.OnInitialize();
                    }
                }
            }
        }
    }
}
