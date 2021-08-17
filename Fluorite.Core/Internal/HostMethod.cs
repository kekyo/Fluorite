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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal abstract class HostMethod
    {
        public abstract ValueTask<object> InvokeAsync(IPayloadContainerView container);

        public static HostMethod Create(IHost host, MethodInfo method)
        {
            Debug.Assert(method.ReturnType.IsValueType());
            Debug.Assert(method.ReturnType.IsGenericType());
            Debug.Assert(method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>));

            var vt = method.ReturnType;
            var vtet = vt.GetGenericArguments()[0];

            var hmt = typeof(HostMethod<>).MakeGenericType(vtet);
            return (HostMethod)Activator.CreateInstance(hmt, host, method)!;
        }
    }

    internal sealed class HostMethod<TResult> : HostMethod
    {
        private readonly IHost host;
        private readonly MethodInfo method;

        public HostMethod(IHost host, MethodInfo method)
        {
            this.host = host;
            this.method = method;
        }

        public override async ValueTask<object> InvokeAsync(IPayloadContainerView container)
        {
            // TODO: improve
            var args = await Task.WhenAll(
                method.GetParameters().
                    Select(p => container.DeserializeDataAsync(p.Position, p.ParameterType).AsTask()));

            var result = await ((ValueTask<TResult>)this.method.Invoke(this.host, args)!).
                ConfigureAwait(false);
            return result!;
        }
    }
}
