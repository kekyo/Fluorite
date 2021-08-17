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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluorite.Internal
{
    internal abstract class Awaiter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected Awaiter()
        {
        }

        public abstract Type ResultType { get; }

        public abstract void SetResult(object? result);
        public abstract void SetException(Exception ex);
        public abstract void SetCanceled();
    }

    internal sealed class Awaiter<TData> : Awaiter
    {
        private readonly TaskCompletionSource<TData> tcs = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Awaiter()
        {
        }

        public Task<TData> Task =>
            this.tcs.Task;

        public override Type ResultType =>
            typeof(TData);

        public override void SetResult(object? result) =>
            this.tcs.TrySetResult((TData)result!);
        public override void SetException(Exception ex) =>
            this.tcs.TrySetException(ex);
        public override void SetCanceled() =>
            this.tcs.TrySetCanceled();
    }
}
