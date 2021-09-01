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

using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Fluorite
{
#if !CITest
    [TestFixture]
    public sealed class WebSocketTest
    {
        // netsh http add urlacl url=http://+:4649/ user=everyone
        // netsh advfirewall firewall add rule name="Fluorite.Tests HTTP" dir=in action=allow
        // netsh advfirewall firewall set rule name="Fluorite.Tests HTTP" new program=system profile=private protocol=tcp localport=4649

        private const int IterationCount = 10000;

        public interface ITestInterface1 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public interface ITestInterface4 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, byte[] arg2);
        }

        public interface ITestInterface5 : IHost
        {
            ValueTask<byte[]> Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public sealed class TestClass1 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"1: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass2 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"2: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass6 : ITestInterface4
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, byte[] arg2)
            {
                var pcrc = arg2.Aggregate(0, (agg, v) => (agg << 4) ^ v);
                return new ValueTask<string>($"6: {arg0} - {arg1} - {arg2.Length}:{pcrc}");
            }
        }

        public sealed class TestClass7 : ITestInterface5
        {
            public static byte[] Create(int arg0, string arg1, DateTime arg2)
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                for (var index = 0; index < 10000; index++)
                {
                    bw.Write(arg0);
                    bw.Write(arg1);
                    bw.Write(arg2.Ticks);
                }
                bw.Flush();
                return ms.ToArray();
            }

            public ValueTask<byte[]> Test1Async(int arg0, string arg1, DateTime arg2) =>
                new ValueTask<byte[]>(Create(arg0, arg1, arg2));
        }

        [SetUp]
        public void SetUp() =>
            Nest.Factory.Initialize();

        [Test]
        public async Task InvokeUniDirectional()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass1());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false);
                try
                {
                    var now = DateTime.Now;
                    var result1 = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

                    Assert.AreEqual($"1: 123 - ABC - {now}", result1);
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }

        [Test]
        public async Task InvokeBiDirectional()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass1());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false);
                client.Register(new TestClass2());
                try
                {
                    var now = DateTime.Now;
                    var result1 = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);
                    var result2 = await server.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

                    Assert.AreEqual($"1: 123 - ABC - {now}", result1);
                    Assert.AreEqual($"2: 123 - ABC - {now}", result2);
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }

        [Test]
        public async Task InvokeTrulyParallelUniDirectional()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass1());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false);
                try
                {
                    var results = await Task.WhenAll(
                        Enumerable.Range(0, IterationCount).
                        Select(index => Task.Run(async () =>
                        {
                            var now = DateTime.Now;
                            var result = await client.GetPeer<ITestInterface1>().
                                Test1Async(index, "ABC", now).
                                ConfigureAwait(false);
                            return (index, now, result);
                        })));

                    foreach (var entry in results)
                    {
                        Assert.AreEqual($"1: {entry.index} - ABC - {entry.now}", entry.result);
                    }
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }

        [Test]
        public async Task InvokeTrulyParallelBiDirectional()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass1());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false, new TestClass2());
                try
                {
                    var results = await Task.WhenAll(
                        Enumerable.Range(0, IterationCount).
                        Select(index => Task.Run(async () =>
                        {
                            var now = DateTime.Now;
                            var result = ((index % 2) == 0) ?
                                await client.GetPeer<ITestInterface1>().
                                    Test1Async(index, "ABC", now).
                                    ConfigureAwait(false) :
                                await server.GetPeer<ITestInterface1>().
                                    Test1Async(index, "ABC", now).
                                    ConfigureAwait(false);
                            return (index, now, result);
                        })));

                    foreach (var entry in results)
                    {
                        if ((entry.index % 2) == 0)
                        {
                            Assert.AreEqual($"1: {entry.index} - ABC - {entry.now}", entry.result);
                        }
                        else
                        {
                            Assert.AreEqual($"2: {entry.index} - ABC - {entry.now}", entry.result);
                        }
                    }
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }

        [Test]
        public async Task LargeDataInArgument()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass6());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false);
                try
                {
                    var data = new byte[100000];
                    var r = new Random();
                    r.NextBytes(data);
                    var result = await client.GetPeer<ITestInterface4>().Test1Async(300, "ABC", data);

                    var pcrc = data.Aggregate(0, (agg, v) => (agg << 4) ^ v);
                    Assert.AreEqual($"6: 300 - ABC - {data.Length}:{pcrc}", result);
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }

        [Test]
        public async Task LargeDataInResult()
        {
            var server = Nest.Factory.StartServer(4649, false, new TestClass7());
            try
            {
                var client = await Nest.Factory.ConnectAsync("127.0.0.1", 4649, false);
                try
                {
                    var now = DateTime.Now;
                    var result = await client.GetPeer<ITestInterface5>().Test1Async(300, "ABC", now);

                    var data = TestClass7.Create(300, "ABC", now);
                    Assert.AreEqual(data, result);
                }
                finally
                {
                    await client.ShutdownAsync();
                }
            }
            finally
            {
                await server.ShutdownAsync();
            }
        }
    }
#endif
}
