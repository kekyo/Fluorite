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

namespace Fluorite
{
    /// <summary>
    /// The unit. It will be deleted when supported void method.
    /// </summary>
    public struct Unit : IEquatable<Unit>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() =>
            0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) =>
            obj is Unit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Unit obj) =>
            true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IEquatable<Unit>.Equals(Unit other) =>
            true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() =>
            "()";
    }
}
