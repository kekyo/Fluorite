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
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Fluorite.WebSockets
{
    /// <summary>
    /// Fluorite WebSocket server transport controller.
    /// </summary>
    /// <remarks>It will be useful server side implementation with System.Net.WebScokets.WebSocket.</remarks>
    public sealed class WebSocketServerController
    {
        private const int BufferElementSize = 16384;

        private readonly Dictionary<WebSocket, WebSocketController> connections = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        private WebSocketServerController()
        {
        }

        /// <summary>
        /// Run server side handler.
        /// </summary>
        /// <param name="webSocket">WebSocket instance</param>
        /// <param name="shutdownTask">Shutdown awaiting task</param>
        /// <param name="messageType">WebSocket message type</param>
        /// <param name="receivedAction">Calling when WebSocket was received a raw data</param>
        /// <example>
        /// <code>
        /// // A simple core implementation for supporting ASP.NET Core server side
        /// // with delegation WebSocketServerController class.
        /// var controller = WebSocketServerController.Create();
        /// 
        /// // https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.useextensions.use
        /// applicationBuilder.Use(async (context, next) =>
        /// {
        ///    if (context.Request.Path == "/ws")
        ///    {
        ///        if (context.WebSockets.IsWebSocketRequest)
        ///        {
        ///            using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
        ///            {
        ///                // shutdownTask, messageType and receivedAction become from ITransport implementation.
        ///                await controller.RunAsync(webSocket, shutdownTask, messageType, receivedAction);
        ///            }
        ///        }
        ///        else
        ///        {
        ///            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        ///        }
        ///    }
        ///    else
        ///    {
        ///        await next();
        ///    }
        /// });
        /// </code>
        /// </example>
        public async ValueTask RunAsync(
            WebSocket webSocket,
            Task shutdownTask,
            WebSocketMessageType messageType,
            Func<Stream, ValueTask> receivedAction)
        {
            using (var controller = new WebSocketController(
                webSocket, messageType, BufferElementSize))
            {
                lock (this.connections)
                {
                    this.connections.Add(webSocket, controller);
                }

                try
                {
                    await controller.RunAsync(receivedAction, shutdownTask).
                        ConfigureAwait(false);
                }
                finally
                {
                    lock (this.connections)
                    {
                        this.connections.Remove(webSocket);
                    }
                    webSocket.Dispose();
                }
            }
        }

        /// <summary>
        /// Will begin sending and get sender stream.
        /// </summary>
        /// <returns>Stream</returns>
        public ValueTask<Stream> GetSenderStreamAsync()
        {
            // TODO: Broadcast all connections.
            WebSocketController[] controllers;
            lock (this.connections)
            {
                controllers = this.connections.Values.
                    ToArray();
            }

            if (controllers.Length >= 1)
            {
                var bridges = controllers.
                    Select(controller => controller.AllocateSenderStreamBridge()).
                    ToArray();
                return new ValueTask<Stream>(new SenderStream(bridges));
            }

            throw new ObjectDisposedException("WebSocketServerTransport already shutdowned.");
        }

        /// <summary>
        /// Create WebSocketServerController instance.
        /// </summary>
        /// <returns>WebSocketServerController</returns>
        public static WebSocketServerController Create() =>
            new WebSocketServerController();
    }
}
