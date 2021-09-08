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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal abstract class HostMethodBase
    {
        private protected SynchronizationContext? synchContext;
        private protected IHost? host;
        private protected MethodInfo? method;

        public abstract ValueTask<object?> InvokeAsync(IPayloadContainerView container);

        public static HostMethodBase Create(IHost host, MethodInfo method, SynchronizationContext? synchContext)
        {
            Debug.Assert(method.ReturnType.IsValueType());
            Debug.Assert(
                (method.ReturnType.IsGenericType() &&
                 method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)) ||
                 method.ReturnType == typeof(ValueTask));

            var vt = method.ReturnType;
            var vtet = vt.GetGenericArguments().ElementAtOrDefault(0);

            HostMethodBase stub;
            if (vtet != null)
            {
                var stubType = typeof(HostMethod<>).MakeGenericType(vtet);
                stub = (HostMethodBase)Activator.CreateInstance(stubType)!;
            }
            else
            {
                stub = new HostMethod();
            }

            stub.synchContext = synchContext;
            stub.host = host;
            stub.method = method;

            return stub;
        }
    }

    internal sealed class HostMethod : HostMethodBase
    {
        public override async ValueTask<object?> InvokeAsync(IPayloadContainerView container)
        {
            Debug.Assert(this.host != null);
            Debug.Assert(this.method != null);

            // TODO: improve
            if (this.synchContext is { } synchContext &&
                !object.ReferenceEquals(synchContext, SynchronizationContext.Current))
            {
                var args = await Task.WhenAll(
                    this.method!.GetParameters().
                        Select(p => container.DeserializeBodyAsync(p.Position, p.ParameterType).AsTask())).
                    ConfigureAwait(false);

                var tcs = new TaskCompletionSource<object?>();
                synchContext.Post(async _ =>
                {
                    try
                    {
                        await ((ValueTask)this.method.Invoke(this.host!, args)!).
                            ConfigureAwait(false)!;
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, null);

                return (await tcs.Task.
                    ConfigureAwait(false))!;
            }
            else
            {
                var args = await Task.WhenAll(
                    this.method!.GetParameters().
                        Select(p => container.DeserializeBodyAsync(p.Position, p.ParameterType).AsTask()));

                await ((ValueTask)this.method.Invoke(this.host!, args)!).
                    ConfigureAwait(false);

                return null;
            }
        }
    }

    internal sealed class HostMethod<TResult> : HostMethodBase
    {
        public override async ValueTask<object?> InvokeAsync(IPayloadContainerView container)
        {
            Debug.Assert(this.host != null);
            Debug.Assert(this.method != null);

            // TODO: improve
            if (this.synchContext is { } synchContext &&
                !object.ReferenceEquals(synchContext, SynchronizationContext.Current))
            {
                var args = await Task.WhenAll(
                    this.method!.GetParameters().
                        Select(p => container.DeserializeBodyAsync(p.Position, p.ParameterType).AsTask())).
                    ConfigureAwait(false);

                var tcs = new TaskCompletionSource<TResult>();
                synchContext.Post(async _ =>
                {
                    try
                    {
                        var result = await ((ValueTask<TResult>)this.method.Invoke(this.host!, args)!).
                            ConfigureAwait(false)!;
                        tcs.TrySetResult(result);
                    }
                    // Requires for reflection invocation.
                    catch (TargetInvocationException ex)
                    {
                        Debug.Assert(ex.InnerException != null);
                        tcs.TrySetException(ex.InnerException!);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, null);

                return (await tcs.Task.
                    ConfigureAwait(false))!;
            }
            else
            {
                var args = await Task.WhenAll(
                    this.method!.GetParameters().
                        Select(p => container.DeserializeBodyAsync(p.Position, p.ParameterType).AsTask()));

                try
                {
                    return (await ((ValueTask<TResult>)this.method.Invoke(this.host!, args)!).
                        ConfigureAwait(false))!;
                }
                // Requires for reflection invocation.
                catch (TargetInvocationException ex)
                {
                    Debug.Assert(ex.InnerException != null);
                    var edi = ExceptionDispatchInfo.Capture(ex.InnerException!);
                    edi.Throw();

                    // HACK: Will not execute.
                    throw ex.InnerException!;
                }
            }
        }
    }
}
