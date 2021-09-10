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

using Fluorite.Internal;
using System;
using System.Linq;

namespace Fluorite.Serialization
{
    /// <summary>
    /// Exception information class.
    /// </summary>
    /// <remarks>Translated logical exception information.
    /// Uses directly serialize/deserialize target when be applicable your serializer,
    /// or you have to implement custom serializing onto ISerializer.SerializeExceptionAsync() and
    /// IPayloadContainerView.DeserializeBodyAsync().</remarks>
#if !NETSTANDARD1_3
    [Serializable]
#endif
    public sealed class ExceptionInformation : IExceptionInformationView
    {
#if NETFRAMEWORK
        private static readonly bool isRunningOnOldCLR =
            (Environment.OSVersion.Platform == PlatformID.Win32NT) &&
            (System.Type.GetType("Mono.Runtime") == null);
#endif
        private static readonly ExceptionInformation[] empty = new ExceptionInformation[0];

        /// <summary>
        /// Peer exception type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Peer exception message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Peer stack trace if available.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Inner exceptions.
        /// </summary>
        public ExceptionInformation[] InnerExceptions { get; set; }

        /// <summary>
        /// Inner exceptions.
        /// </summary>
        IExceptionInformationView[] IExceptionInformationView.InnerExceptions =>
            this.InnerExceptions;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ExceptionInformation()
        {
            this.Type = "(Unknown)";
            this.Message = "(Nothing)";
            this.InnerExceptions = empty;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">Peer exception type</param>
        /// <param name="message">Message string</param>
        public ExceptionInformation(string type, string message)
        {
            this.Type = type;
            this.Message = message;
            this.InnerExceptions = empty;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ex">Original exception</param>
        /// <param name="containsStackTrace">Perform includes stack trace</param>
        public ExceptionInformation(Exception ex, bool containsStackTrace)
        {
            this.Type = ProxyUtilities.GetTypeIdentity(ex.GetType());
            this.InnerExceptions = ex is AggregateException aex ?
                aex.InnerExceptions.Select(ex => new ExceptionInformation(ex, containsStackTrace)).ToArray() :
                ex.InnerException is { } iex ?
                    new[] { new ExceptionInformation(iex, containsStackTrace) } : empty;
            this.Message = GetStrictMessageFromException(ex);
            this.StackTrace = containsStackTrace ? ex.StackTrace : null;
        }

        /// <summary>
        /// Get strict message string from targetted Exception class.
        /// </summary>
        /// <param name="ex">Target exception</param>
        /// <returns>Message string</returns>
        public static string GetStrictMessageFromException(Exception ex)
        {
#if NETFRAMEWORK
            if (isRunningOnOldCLR)
            {
                return ex.Message;
            }
#endif

            if (!(ex is AggregateException aex))
            {
                return ex.Message;
            }

            // DIRTY HACK: On .NET Core CLR and mono, the Message property will fold covering
            //   all nested exception with brackets into a string (maybe makes better human readable).
            //   Fluorite unfolds it...
            return aex.InnerExceptions.Aggregate(
                ex.Message,
                (agg, v) => agg.Replace($" ({v.Message})", string.Empty));
        }
    }
}
