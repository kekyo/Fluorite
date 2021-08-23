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

using Fluorite.Serialization;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal abstract class MethodStub
    {
        private protected IHost? host;
        private protected MethodInfo? method;
        private protected SynchronizationContext? synchContext;

        public abstract ValueTask<object> InvokeAsync(IPayloadContainerView container);

        public static MethodStub Create(IHost host, MethodInfo method, SynchronizationContext? synchContext)
        {
            Debug.Assert(method.ReturnType.IsValueType());
            Debug.Assert(method.ReturnType.IsGenericType());
            Debug.Assert(method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>));

            var vt = method.ReturnType;
            var vtet = vt.GetGenericArguments()[0];

            var stubType = typeof(HostMethod<>).MakeGenericType(vtet);
            var stub = (MethodStub)Activator.CreateInstance(stubType)!;
            
            stub.host = host;
            stub.method = method;
            stub.synchContext = synchContext;

            return stub;
        }
    }

    internal sealed class HostMethod<TResult> : MethodStub
    {
        public override async ValueTask<object> InvokeAsync(IPayloadContainerView container)
        {
            Debug.Assert(this.host != null);
            Debug.Assert(this.method != null);

            // TODO: improve
            var args = await Task.WhenAll(
                this.method!.GetParameters().
                    Select(p => container.DeserializeDataAsync(p.Position, p.ParameterType).AsTask())).
                ConfigureAwait(false);

            if (this.synchContext != null)
            {
                var tcs = new TaskCompletionSource<object>();

                this.synchContext.Post(async _ =>
                {
                    try
                    {
                        var result = (await ((ValueTask<TResult>)this.method.Invoke(this.host!, args)!).
                            ConfigureAwait(false))!;
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                null);

                return await tcs.Task.
                    ConfigureAwait(false);
            }
            else
            {
                return (await ((ValueTask<TResult>)this.method.Invoke(this.host!, args)!).
                    ConfigureAwait(false))!;
            }
        }
    }
}