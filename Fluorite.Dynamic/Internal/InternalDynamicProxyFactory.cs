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

using System.Runtime.CompilerServices;

namespace Fluorite.Internal
{
    internal abstract class InternalDynamicProxyFactory
    {
        private protected InternalDynamicProxyFactory()
        {
        }

        public abstract T CreateInstance<T>(Nest test)
            where T : class, IHost;
    }

    internal sealed class InternalDynamicProxyFactory<TPeer, TProxy> :
        InternalDynamicProxyFactory
        where TPeer : class, IHost
        where TProxy : DynamicProxyBase, new()
    {
        public InternalDynamicProxyFactory()
        {
        }

        public override T CreateInstance<T>(Nest nest)
        {
            var proxy = new TProxy();
            proxy.nest = nest;
            return (T)(IHost)proxy;
        }
    }

    internal static class InternalDynamicProxyFactory<TPeer>
        where TPeer : class, IHost
    {
        private static readonly InternalDynamicProxyFactory factory =
            InternalDynamicProxyGenerator.CreateProxyFactory<TPeer>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPeer CreateInstance(Nest nest) =>
            factory!.CreateInstance<TPeer>(nest);
    }
}
