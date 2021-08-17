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

namespace Fluorite.Serialization
{
#if !NETCOREAPP1_0 && !NETSTANDARD1_3
    [Serializable]
#endif
    public abstract class PayloadContainerBase : IPayloadContainerView
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PayloadContainerBase()
        {
            this.Identity = default!;
            this.Name = default!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PayloadContainerBase(Guid identity, string name)
        {
            this.Identity = identity.ToString("N");
            this.Name = name;
        }

        public string Identity { get; set; }

        Guid IPayloadContainerView.Identity =>
            new Guid(this.Identity);

        public string Name { get; set; }

        public abstract int DataCount { get; }

        public abstract ValueTask<object?> DeserializeDataAsync(int index, Type type);

        public override string ToString() =>
            $"{this.Identity}: {this.Name}";
    }
}
