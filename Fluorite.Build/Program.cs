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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Fluorite
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var isTrace = false;
            void Message(LogLevels level, string message)
            {
                switch (level)
                {
                    case LogLevels.Information:
                        Console.WriteLine($"Fluorite.Build: {message}");
                        break;
                    case LogLevels.Trace when !isTrace:
                        break;
                    default:
                        Console.WriteLine($"Fluorite.Build: {level.ToString().ToLowerInvariant()}: {message}");
                        break;
                }
            }

            try
            {
                switch (args[0])
                {
                    // Before: Generate Initializer
                    case "-gi":
                        var targetSourceCodePath = args[1];
                        var resourceName = $"Fluorite.Resources.{Path.GetFileName(targetSourceCodePath)}";
                        isTrace = args.ElementAtOrDefault(2) is { } arg2 && bool.TryParse(arg2, out var v2) && v2;

                        using (var rs = typeof(Program).Assembly.GetManifestResourceStream(resourceName)!)
                        {
                            using (var fs = new FileStream(targetSourceCodePath,
                                FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                            {
                                rs.CopyTo(fs);
                                fs.Flush();
                            }
                        }
                        break;

                    // After: Generate Proxy
                    case "-gp":
                        var referencesBasePath = args[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        var targetAssemblyPath = args[2];
                        isTrace = args.ElementAtOrDefault(3) is { } arg3 && bool.TryParse(arg3, out var v3) && v3;

                        var generator = new StaticProxyGenerator(referencesBasePath, targetAssemblyPath, Message);

                        if (generator.Inject())
                        {
                            Message(
                                LogLevels.Information,
                                $"Injected target assembly: Assembly={Path.GetFileName(targetAssemblyPath)}");
                        }
                        else
                        {
                            Message(LogLevels.Information,
                                $"Injection target isn't found: Assembly={Path.GetFileName(targetAssemblyPath)}");
                        }

                        break;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Message(LogLevels.Error, $"{ex.GetType().Name}: {ex.Message}");
                return Marshal.GetHRForException(ex);
            }
        }
    }
}
