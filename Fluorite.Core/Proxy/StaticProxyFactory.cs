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

using Fluorite.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Fluorite.Proxy
{
    public sealed class StaticProxyFactory :
        IPeerProxyFactory
    {
        private readonly Dictionary<Type, Func<Nest, IHost>> generators = new();

        private StaticProxyFactory()
        {
        }

        private void InternalRegister<TPeer, TProxy>()
            where TPeer : class, IHost
            where TProxy : StaticProxyBase, new()
        {
            var type = typeof(TPeer);
            if (!type.IsInterface())
            {
                throw new ArgumentException();
            }

            lock (this.generators)
            {
                this.generators[type] = nest =>
                    { var proxy = new TProxy(); proxy.nest = nest; return proxy; };
            }
        }

        private void InternalUnregister<TPeer>()
        {
            var type = typeof(TPeer);
            if (!type.IsInterface())
            {
                throw new ArgumentException();
            }

            lock (this.generators)
            {
                this.generators.Remove(type);
            }
        }

        public TPeer CreateInstance<TPeer>(Nest nest)
            where TPeer : class, IHost
        {
            var type = typeof(TPeer);
            if (!type.IsInterface())
            {
                throw new ArgumentException();
            }

            Func<Nest, IHost>? generator;
            lock (this.generators)
            {
                if (!this.generators.TryGetValue(type, out generator))
                {
                    throw new ArgumentException();
                }
            }

            return (TPeer)generator(nest);
        }

        public static readonly StaticProxyFactory Instance =
            new StaticProxyFactory();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register<TPeer, TProxy>()
            where TPeer : class, IHost
            where TProxy : StaticProxyBase, new() =>
            Instance.InternalRegister<TPeer, TProxy>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unregister<TPeer>() =>
            Instance.InternalUnregister<TPeer>();

        internal static string GetInterfaceNames(IHost proxy) =>
            string.Join(
                ", ",
                proxy.GetType().
                GetInterfaces().
                Where(t => (t != typeof(IHost)) && typeof(IHost).IsAssignableFrom(t)).
                Select(t => t.FullName));
    }
}