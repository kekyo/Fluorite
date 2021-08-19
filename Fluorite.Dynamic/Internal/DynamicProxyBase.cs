﻿////////////////////////////////////////////////////////////////////////////
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

using Fluorite.Proxy;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class DynamicProxyBase : IHost
    {
        internal Nest? nest;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected DynamicProxyBase()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask<TResult> InvokeAsync<TResult>(string fullName, object[] args) =>
            this.nest!.InvokeAsync<TResult>(fullName, args);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString() =>
            $"Fluorite dynamic proxy: {StaticProxyFactory.GetInterfaceNames(this)}";
    }
}