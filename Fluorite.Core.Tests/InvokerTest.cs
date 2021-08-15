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
using Fluorite.Json;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Fluorite
{
    public interface ITestInterface
    {
        ValueTask<string> Test1(int arg0, string arg1, DateTime arg2);
    }

    public sealed class TestClass : ITestInterface
    {
        public ValueTask<string> Test1(int arg0, string arg1, DateTime arg2)
        {
            return new ValueTask<string>($"{arg0} - {arg1} - {arg2}");
        }
    }

    public sealed class TestTransport : ITransport
    {
        public ValueTask SendAsync(ArraySegment<byte> data)
        {
            throw new InvalidOperationException();
        }

        public IDisposable Subscribe(IObserver<ArraySegment<byte>> observer) =>
            new DummyDisposable();

        private sealed class DummyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [TestFixture]
    public sealed class InvokerTest
    {
        [Test]
        public void Invoke()
        {
            var invoker = Nest.Factory.Create<ITestInterface>(
                NestSettings.Create(JsonSerializer.Instance, new TestTransport()));

            var now = DateTime.Now;
            var result = invoker.Peer.Test1(123, "ABC", now);

            Assert.AreEqual($"123 - ABC - {now}", result);
        }
    }
}
