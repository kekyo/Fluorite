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

using Fluorite.Advanced;
using System;
using System.ComponentModel;
using System.Linq;

namespace Fluorite.Internal
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ProxyUtilities
    {
        internal static string GetInterfaceNames(IHost proxy) =>
            string.Join(
                ", ",
                proxy.GetType().
                GetInterfaces().
                Where(t => (t != typeof(IHost)) && typeof(IHost).IsAssignableFrom(t)).
                Select(t => t.FullName));

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetTypeIdentity(Type type) =>
            type.FullName!.Replace('+', '.').Replace('/', '.');

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetMethodIdentity(Type type, string methodName)
        {
            var name = methodName.EndsWith("Async") ?
                methodName.Substring(0, methodName.Length - 5) :
                methodName;
            return $"{GetTypeIdentity(type)}.{name}";
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetMethodIdentity<TPeer>(string methodName) =>
            GetMethodIdentity(typeof(TPeer), methodName);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkInitialized() =>
            NestFactoryBasisExtension.MarkInitialized();
    }
}
