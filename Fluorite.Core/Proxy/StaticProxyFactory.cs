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

using Fluorite.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fluorite.Proxy
{
    /// <summary>
    /// Static transparent proxy factory class.
    /// </summary>
    public sealed class StaticProxyFactory :
        IPeerProxyFactory
    {
        private readonly Dictionary<Type, Func<Nest, IHost>> generators = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        private StaticProxyFactory()
        {
        }

        /// <summary>
        /// Register proxy class type.
        /// </summary>
        /// <typeparam name="TPeer">Target expose interface type</typeparam>
        /// <typeparam name="TProxy">Proxy class type</typeparam>
        private void InternalRegister<TPeer, TProxy>()
            where TPeer : class, IHost
            where TProxy : ProxyBase, new()
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

        /// <summary>
        /// Unregister proxy class type.
        /// </summary>
        /// <typeparam name="TPeer">Target expose interface type</typeparam>
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

        /// <summary>
        /// Create transparent proxy instance.
        /// </summary>
        /// <typeparam name="TPeer">Exposed interface type</typeparam>
        /// <param name="nest">Nest instance</param>
        /// <returns>Proxy instance</returns>
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

        /// <summary>
        /// Static transparent proxy factory instance.
        /// </summary>
        public static readonly StaticProxyFactory Instance =
            new StaticProxyFactory();

        /// <summary>
        /// Register proxy class type.
        /// </summary>
        /// <typeparam name="TPeer">Target expose interface type</typeparam>
        /// <typeparam name="TProxy">Proxy class type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register<TPeer, TProxy>()
            where TPeer : class, IHost
            where TProxy : ProxyBase, new() =>
            Instance.InternalRegister<TPeer, TProxy>();

        /// <summary>
        /// Unregister proxy class type.
        /// </summary>
        /// <typeparam name="TPeer">Target expose interface type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unregister<TPeer>() =>
            Instance.InternalUnregister<TPeer>();
    }
}
