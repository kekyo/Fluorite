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
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite
{
    [TestFixture]
    public sealed class NestTest
    {
#if NETFRAMEWORK
        private static readonly bool isRunningOnOldCLR =
            (Environment.OSVersion.Platform == PlatformID.Win32NT) &&
            (Type.GetType("Mono.Runtime") == null);
#endif

        private const int IterationCount = 10000;

        public interface ITestInterface1 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public interface ITestInterface2 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public interface ITestInterface3 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2);
            ValueTask<string> Test2Async(int arg0, string arg1, DateTime arg2);
        }

        public interface ITestInterface4 : IHost
        {
            ValueTask<string> Test1Async(int arg0, string arg1, byte[] arg2);
        }

        public interface ITestInterface5 : IHost
        {
            ValueTask<byte[]> Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public interface ITestInterface6 : IHost
        {
            ValueTask Test1Async(int arg0, string arg1, DateTime arg2);
        }

        public sealed class TestClass1_1 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"11: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass1_2 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"12: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass2_1 : ITestInterface2
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"21: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass2_2 : ITestInterface2
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"22: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass3 : ITestInterface3
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"31: {arg0} - {arg1} - {arg2}");
            }
            public ValueTask<string> Test2Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"32: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass4_1 : ITestInterface1
        {
            public async ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                await Task.Delay(arg0);
                return $"41: {arg0} - {arg1} - {arg2}";
            }
        }

        public sealed class TestClass4_2 : ITestInterface1
        {
            public async ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                await Task.Delay(arg0).ConfigureAwait(false);
                return $"42: {arg0} - {arg1} - {arg2}";
            }
        }

        public sealed class TestClass5_1 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"51:{Thread.CurrentThread.ManagedThreadId}: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass5_2 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"52:{Thread.CurrentThread.ManagedThreadId}: {arg0} - {arg1} - {arg2}");
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

        public sealed class TestClass8 : ITestInterface6
        {
            public string? Value;

            public async ValueTask Test1Async(int arg0, string arg1, DateTime arg2)
            {
                this.Value = "AAA";
                await Task.Delay(500);
                this.Value = $"6: {arg0} - {arg1} - {arg2}";
            }
        }

        public sealed class TestClass9_1 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                throw new ArgumentException($"91: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass9_2 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                throw new ArgumentException(
                    $"921: {arg0} - {arg1} - {arg2}",
                    new AggregateException(
                        $"922: {arg0} - {arg1} - {arg2}",
                        new InvalidOperationException(
                            $"9231: {arg0} - {arg1} - {arg2}"),
                        new NotImplementedException(
                            $"9232: {arg0} - {arg1} - {arg2}")));
            }
        }

        public sealed class TestClass10 : ITestInterface1
        {
            public string? Value;

            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                this.Value = $"101: {arg0} - {arg1} - {arg2}";
                return new ValueTask<string>(default(string)!);
            }
        }

        [Test]
        public async Task InvokeUniDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

            Assert.AreEqual($"11: 123 - ABC - {now}", result);
        }

        [Test]
        public async Task InvokeBiDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());
            client.Register(new TestClass1_2());

            var now = DateTime.Now;
            var result1 = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);
            var result2 = await server.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

            Assert.AreEqual($"11: 123 - ABC - {now}", result1);
            Assert.AreEqual($"12: 123 - ABC - {now}", result2);
        }

        [Test]
        public async Task InvokeDifferentInterface()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());
            server.Register(new TestClass2_1());

            var now = DateTime.Now;
            var result1 = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);
            var result2 = await client.GetPeer<ITestInterface2>().Test1Async(123, "ABC", now);

            Assert.AreEqual($"11: 123 - ABC - {now}", result1);
            Assert.AreEqual($"21: 123 - ABC - {now}", result2);
        }

        [Test]
        public async Task InvokeDifferentMethod()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass3());

            var now = DateTime.Now;
            var result1 = await client.GetPeer<ITestInterface3>().Test1Async(123, "ABC", now);
            var result2 = await client.GetPeer<ITestInterface3>().Test2Async(123, "ABC", now);

            Assert.AreEqual($"31: 123 - ABC - {now}", result1);
            Assert.AreEqual($"32: 123 - ABC - {now}", result2);
        }

        [Test]
        public async Task InvokePseudoParallelUniDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());

            var results = await Task.WhenAll(
                Enumerable.Range(0, IterationCount).
                Select(async index =>
                {
                    var now = DateTime.Now;
                    var result = await client.GetPeer<ITestInterface1>().
                        Test1Async(index, "ABC", now).
                        ConfigureAwait(false);
                    return (index, now, result);
                }));

            foreach (var entry in results)
            {
                Assert.AreEqual($"11: {entry.index} - ABC - {entry.now}", entry.result);
            }
        }

        [Test]
        public async Task InvokePseudoParallelBiDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());
            client.Register(new TestClass1_2());

            var results = await Task.WhenAll(
                Enumerable.Range(0, IterationCount).
                Select(async index =>
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
                }));

            foreach (var entry in results)
            {
                if ((entry.index % 2) == 0)
                {
                    Assert.AreEqual($"11: {entry.index} - ABC - {entry.now}", entry.result);
                }
                else
                {
                    Assert.AreEqual($"12: {entry.index} - ABC - {entry.now}", entry.result);
                }
            }
        }

        [Test]
        public async Task InvokeTrulyParallelUniDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());

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
                Assert.AreEqual($"11: {entry.index} - ABC - {entry.now}", entry.result);
            }
        }

        [Test]
        public async Task InvokeTrulyParallelBiDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass1_1());
            client.Register(new TestClass1_2());

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
                    Assert.AreEqual($"11: {entry.index} - ABC - {entry.now}", entry.result);
                }
                else
                {
                    Assert.AreEqual($"12: {entry.index} - ABC - {entry.now}", entry.result);
                }
            }
        }

        [Test]
        public void InvokePseudoParallelUniDirectionalWithSynchContext()
        {
            var sc = new ThreadBoundSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);

            var tid = Thread.CurrentThread.ManagedThreadId;

            async Task ExecuteAsync()
            {
                var (server, client) = Utilities.CreateDirectAttachedNestPair();
                server.Register(new TestClass5_1());

                var results = await Task.WhenAll(
                    Enumerable.Range(0, IterationCount).
                    Select(async index =>
                    {
                        var now = DateTime.Now;
                        var result = await client.GetPeer<ITestInterface1>().
                            Test1Async(index, "ABC", now).
                            ConfigureAwait(false);
                        return (index, now, result);
                    }));

                foreach (var entry in results)
                {
                    Assert.AreEqual($"51:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                }
            }

            sc.Run(ExecuteAsync());
        }

        [Test]
        public void InvokePseudoParallelBiDirectionalWithSynchContext()
        {
            var sc = new ThreadBoundSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);

            var tid = Thread.CurrentThread.ManagedThreadId;

            async Task ExecuteAsync()
            {
                var (server, client) = Utilities.CreateDirectAttachedNestPair();
                server.Register(new TestClass5_1());
                client.Register(new TestClass5_2());

                var results = await Task.WhenAll(
                    Enumerable.Range(0, IterationCount).
                    Select(async index =>
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
                    }));

                foreach (var entry in results)
                {
                    if ((entry.index % 2) == 0)
                    {
                        Assert.AreEqual($"51:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                    }
                    else
                    {
                        Assert.AreEqual($"52:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                    }
                }
            }

            sc.Run(ExecuteAsync());
        }

        [Test]
        public void InvokeTrulyParallelUniDirectionalWithSynchContext()
        {
            var sc = new ThreadBoundSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);

            var tid = Thread.CurrentThread.ManagedThreadId;

            async Task ExecuteAsync()
            {
                var (server, client) = Utilities.CreateDirectAttachedNestPair();
                server.Register(new TestClass5_1());

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
                    Assert.AreEqual($"51:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                }
            }

            sc.Run(ExecuteAsync());
        }

        [Test]
        public void InvokeTrulyParallelBiDirectionalWithSynchContext()
        {
            var sc = new ThreadBoundSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);

            var tid = Thread.CurrentThread.ManagedThreadId;

            async Task ExecuteAsync()
            {
                var (server, client) = Utilities.CreateDirectAttachedNestPair();
                server.Register(new TestClass5_1());
                client.Register(new TestClass5_2());

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
                        Assert.AreEqual($"51:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                    }
                    else
                    {
                        Assert.AreEqual($"52:{tid}: {entry.index} - ABC - {entry.now}", entry.result);
                    }
                }
            }

            sc.Run(ExecuteAsync());
        }

        [Test]
        public async Task Delay()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass4_1());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(300, "ABC", now);

            Assert.AreEqual($"41: 300 - ABC - {now}", result);
        }

        [Test]
        public async Task DelayWithReleaseContext()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass4_2());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(300, "ABC", now);

            Assert.AreEqual($"42: 300 - ABC - {now}", result);
        }

        [Test]
        public async Task LargeDataInArgument()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass6());

            var data = new byte[100000];
            var r = new Random();
            r.NextBytes(data);
            var result = await client.GetPeer<ITestInterface4>().Test1Async(300, "ABC", data);

            var pcrc = data.Aggregate(0, (agg, v) => (agg << 4) ^ v);
            Assert.AreEqual($"6: 300 - ABC - {data.Length}:{pcrc}", result);
        }

        [Test]
        public async Task LargeDataOutResult()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass7());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface5>().Test1Async(300, "ABC", now);

            var data = TestClass7.Create(300, "ABC", now);
            Assert.AreEqual(data, result);
        }

        [Test]
        public async Task InvokeVoidReturn()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            var t = new TestClass8();
            server.Register(t);

            var now = DateTime.Now;
            await client.GetPeer<ITestInterface6>().Test1Async(123, "ABC", now);

            Assert.AreEqual($"6: 123 - ABC - {now}", t.Value);
        }

        [Test]
        public async Task InvokeCaughtException()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            var t = new TestClass9_1();
            server.Register(t);

            var now = DateTime.Now;
            try
            {
                await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);
                Assert.Fail();
            }
            catch (PeerException pex)
            {
                Assert.AreEqual("System.ArgumentException", pex.PeerExceptionType);
                Assert.AreEqual($"91: 123 - ABC - {now}", pex.Message);
            }
        }

        [Test]
        public async Task InvokeCaughtNestedException()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            var t = new TestClass9_2();
            server.Register(t);

            var now = DateTime.Now;
            try
            {
                await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);
                Assert.Fail();
            }
            catch (PeerException pex)
            {
                Assert.AreEqual("System.ArgumentException", pex.PeerExceptionType);
#if NETFRAMEWORK
                if (isRunningOnOldCLR)
                    Assert.AreEqual($"921: 123 - ABC - {now}", pex.Message);
                else
#endif
                Assert.AreEqual($"921: 123 - ABC - {now} (922: 123 - ABC - {now} (9231: 123 - ABC - {now}) (9232: 123 - ABC - {now}))", pex.Message);
                Assert.AreEqual(1, pex.InnerExceptions.Count);

                var iex2 = (PeerException)pex.InnerExceptions[0];
                Assert.AreEqual("System.AggregateException", iex2.PeerExceptionType);
#if NETFRAMEWORK
                if (isRunningOnOldCLR)
                    Assert.AreEqual($"922: 123 - ABC - {now}", iex2.Message);
                else
#endif
                Assert.AreEqual($"922: 123 - ABC - {now} (9231: 123 - ABC - {now}) (9232: 123 - ABC - {now})", iex2.Message);
                Assert.AreEqual(2, iex2.InnerExceptions.Count);

                var iex31 = (PeerException)iex2.InnerExceptions[0];
                Assert.AreEqual("System.InvalidOperationException", iex31.PeerExceptionType);
                Assert.AreEqual($"9231: 123 - ABC - {now}", iex31.Message);
                Assert.AreEqual(0, iex31.InnerExceptions.Count);

                var iex32 = (PeerException)iex2.InnerExceptions[1];
                Assert.AreEqual("System.NotImplementedException", iex32.PeerExceptionType);
                Assert.AreEqual($"9232: 123 - ABC - {now}", iex32.Message);
                Assert.AreEqual(0, iex32.InnerExceptions.Count);
            }
        }

        [Test]
        public async Task InvokeNullReturn()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            var t = new TestClass10();
            server.Register(t);

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

            Assert.IsNull(result);
            Assert.AreEqual($"101: 123 - ABC - {now}", t.Value);
        }
    }
}
