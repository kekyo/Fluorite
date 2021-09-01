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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fluorite
{
    [TestFixture]
    public sealed class NestTest
    {
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

        public sealed class TestClass11 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"11: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass12 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"12: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass21 : ITestInterface2
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"21: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass22 : ITestInterface2
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

        public sealed class TestClass41 : ITestInterface1
        {
            public async ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                await Task.Delay(arg0);
                return $"41: {arg0} - {arg1} - {arg2}";
            }
        }

        public sealed class TestClass42 : ITestInterface1
        {
            public async ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                await Task.Delay(arg0).ConfigureAwait(false);
                return $"42: {arg0} - {arg1} - {arg2}";
            }
        }

        public sealed class TestClass51 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"51:{Thread.CurrentThread.ManagedThreadId}: {arg0} - {arg1} - {arg2}");
            }
        }

        public sealed class TestClass52 : ITestInterface1
        {
            public ValueTask<string> Test1Async(int arg0, string arg1, DateTime arg2)
            {
                return new ValueTask<string>($"52:{Thread.CurrentThread.ManagedThreadId}: {arg0} - {arg1} - {arg2}");
            }
        }

        [SetUp]
        public void SetUp() =>
            Nest.Factory.Initialize();

        [Test]
        public async Task InvokeUniDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass11());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(123, "ABC", now);

            Assert.AreEqual($"11: 123 - ABC - {now}", result);
        }

        [Test]
        public async Task InvokeBiDirectional()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass11());
            client.Register(new TestClass12());

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
            server.Register(new TestClass11());
            server.Register(new TestClass21());

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
            server.Register(new TestClass11());

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
            server.Register(new TestClass11());
            client.Register(new TestClass12());

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
            server.Register(new TestClass11());

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
            server.Register(new TestClass11());
            client.Register(new TestClass12());

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
                server.Register(new TestClass51());

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
                server.Register(new TestClass51());
                client.Register(new TestClass52());

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
                server.Register(new TestClass51());

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
                server.Register(new TestClass51());
                client.Register(new TestClass52());

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
            server.Register(new TestClass41());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(300, "ABC", now);

            Assert.AreEqual($"41: 300 - ABC - {now}", result);
        }

        [Test]
        public async Task DelayWithReleaseContext()
        {
            var (server, client) = Utilities.CreateDirectAttachedNestPair();
            server.Register(new TestClass42());

            var now = DateTime.Now;
            var result = await client.GetPeer<ITestInterface1>().Test1Async(300, "ABC", now);

            Assert.AreEqual($"42: 300 - ABC - {now}", result);
        }
    }
}
