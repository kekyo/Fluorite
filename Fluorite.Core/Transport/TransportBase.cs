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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fluorite.Transport
{
    public abstract class TransportBase :
        ITransport
    {
        private readonly List<IObserver<ArraySegment<byte>>> observers = new();

        protected TransportBase()
        {
        }

        protected virtual void SetPayloadContentType(string contentType)
        {
        }

        void ITransport.SetPayloadContentType(string contentType) =>
            this.SetPayloadContentType(contentType);

        protected void OnReceived(ArraySegment<byte> data)
        {
            lock (this.observers)
            {
                foreach (var observer in this.observers.ToArray())
                {
                    try
                    {
                        observer.OnNext(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }
        }

        protected void OnReceiveError(Exception ex)
        {
            lock (this.observers)
            {
                foreach (var observer in this.observers.ToArray())
                {
                    try
                    {
                        observer.OnError(ex);
                    }
                    catch (Exception ex2)
                    {
                        Debug.WriteLine(ex2);
                    }
                }
            }
        }

        protected void OnReceiveFinished()
        {
            lock (this.observers)
            {
                foreach (var observer in this.observers.ToArray())
                {
                    try
                    {
                        observer.OnCompleted();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }
        }

        public abstract ValueTask SendAsync(ArraySegment<byte> data);

        public virtual ValueTask ShutdownAsync() =>
            default;

        public IDisposable Subscribe(IObserver<ArraySegment<byte>> observer)
        {
            lock (this.observers)
            {
                this.observers.Add(observer);
                return new Disposer(this, observer);
            }
        }

        private void InternalDispose(IObserver<ArraySegment<byte>> observer)
        {
            lock (this.observers)
            {
                this.observers.Remove(observer);
            }
        }

        private sealed class Disposer : IDisposable
        {
            private readonly TransportBase parent;
            private readonly IObserver<ArraySegment<byte>> observer;

            public Disposer(TransportBase parent, IObserver<ArraySegment<byte>> observer)
            {
                this.parent = parent;
                this.observer = observer;
            }

            public void Dispose() =>
                this.parent.InternalDispose(this.observer);
        }
    }
}
