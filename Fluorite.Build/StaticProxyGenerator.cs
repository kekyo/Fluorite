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

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Fluorite
{
    public enum LogLevels
    {
        Trace,
        Information,
        Warning,
        Error
    }

    public sealed class StaticProxyGenerator
    {
        private readonly Action<LogLevels, string> message;
        private readonly DefaultAssemblyResolver assemblyResolver = new();

        private readonly TypeSystem typeSystem;

        private readonly TypeDefinition iHostType;
        private readonly TypeDefinition generatedProxyBaseType;
        private readonly MethodDefinition generatedProxyBaseConstructor;
        private readonly MethodDefinition invokeAsyncMethodT;

        public StaticProxyGenerator(string[] referencesBasePath, Action<LogLevels, string> message)
        {
            this.message = message;

            foreach (var referenceBasePath in referencesBasePath)
            {
                this.assemblyResolver.AddSearchDirectory(referenceBasePath);
            }

            var fluoriteCorePath = referencesBasePath.
                Select(basePath => Path.Combine(basePath, "Fluorite.Core.dll")).
                First(File.Exists);

            var fluoriteCoreAssembly = AssemblyDefinition.ReadAssembly(
                fluoriteCorePath,
                new ReaderParameters
                {
                    AssemblyResolver = assemblyResolver,
                }
            );

            this.message(
                LogLevels.Trace,
                $"Fluorite.Core.dll is loaded: Path={fluoriteCorePath}");

            this.typeSystem = fluoriteCoreAssembly.MainModule.TypeSystem;

            this.iHostType = fluoriteCoreAssembly.MainModule.GetType(
                "Fluorite.IHost")!;

            this.generatedProxyBaseType = fluoriteCoreAssembly.MainModule.GetType(
                "Fluorite.Internal.GeneratedProxyBase")!;

            this.generatedProxyBaseConstructor = this.generatedProxyBaseType.Methods.
                First(m => m.IsConstructor);
            this.invokeAsyncMethodT = this.generatedProxyBaseType.Methods.
                First(m => m.Name.StartsWith("InvokeAsync"));
        }

        private static string GetMethodIdentity(TypeReference type, string methodName)
        {
            var name = methodName.EndsWith("Async") ?
                methodName.Substring(0, methodName.Length - 5) :
                methodName;
            return $"{type.FullName.Replace('+', '.').Replace('/', '.')}.{name}";
        }

        private bool InjectIntoType(ModuleDefinition module, TypeDefinition targetType)
        {
            var nss = targetType.FullName.Replace('+', '_').Replace('/', '_').Split('.');
            var ns = string.Join(".", nss.Take(nss.Length - 1));
            var tn = nss.Last() + "Proxy__";
            var proxyType = new TypeDefinition(
                ns,
                tn,
                TypeAttributes.Sealed | TypeAttributes.Class,
                module.ImportReference(this.generatedProxyBaseType));

            var ii = new InterfaceImplementation(
                module.ImportReference(targetType));
            proxyType.Interfaces.Add(ii);

            var dc = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.ImportReference(this.generatedProxyBaseConstructor.ReturnType));
            var dcilp = dc.Body.GetILProcessor();
            dcilp.Append(Instruction.Create(OpCodes.Ldarg_0));
            dcilp.Append(Instruction.Create(OpCodes.Call,
                module.ImportReference(this.generatedProxyBaseConstructor)));
            dcilp.Append(Instruction.Create(OpCodes.Ret));
            proxyType.Methods.Add(dc);

            foreach (var method in targetType.Methods)
            {
                var returnType = method.ReturnType;
                if (!returnType.FullName.StartsWith("System.Threading.Tasks.ValueTask"))
                {
                    this.message(
                        LogLevels.Error,
                        $"Method doesn't have a ValueTask<T> return type: Target={targetType.FullName}.{method.Name}");
                    return false;
                }

                var valueTaskElementType = ((GenericInstanceType)returnType).
                    GenericArguments.FirstOrDefault();
                // TODO: support non generic ValueTask
                if (valueTaskElementType == null)
                {
                    this.message(
                        LogLevels.Error,
                        $"Method doesn't have a ValueTask<T> return type: Target={targetType.FullName}.{method.Name}");
                    return false;
                }

                var proxyParameterTypes = method.Parameters.
                    Select(p => module.ImportReference(p.ParameterType)).
                    ToArray();
                var proxyMethod = new MethodDefinition(
                    method.Name,
                    MethodAttributes.Public,
                    module.ImportReference(method.ReturnType));
                foreach (var parameterType in proxyParameterTypes)
                {
                    proxyMethod.Parameters.Add(new ParameterDefinition(parameterType));
                }

                var ilProcessor = proxyMethod.Body.GetILProcessor();

                ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));

                ilProcessor.Append(Instruction.Create(OpCodes.Ldstr, GetMethodIdentity(targetType, method.Name)));

                ilProcessor.Append(Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)method.Parameters.Count));
                ilProcessor.Append(Instruction.Create(OpCodes.Newarr, module.ImportReference(this.typeSystem.Object)));

                for (sbyte index = 0; index < method.Parameters.Count; index++)
                {
                    var parameterType = method.Parameters[index].ParameterType;

                    ilProcessor.Append(Instruction.Create(OpCodes.Dup));
                    ilProcessor.Append(Instruction.Create(OpCodes.Ldc_I4_S, index));
                    ilProcessor.Append(Instruction.Create(OpCodes.Conv_I));
                    ilProcessor.Append(Instruction.Create(OpCodes.Ldarg, proxyMethod.Parameters[index]));
                    if (parameterType.IsValueType)
                    {
                        ilProcessor.Append(Instruction.Create(OpCodes.Box, parameterType));
                    }
                    ilProcessor.Append(Instruction.Create(OpCodes.Stelem_Ref));
                }

                var invokeAsyncMethod = new GenericInstanceMethod(
                    module.ImportReference(this.invokeAsyncMethodT));
                invokeAsyncMethod.GenericArguments.Add(
                    module.ImportReference(valueTaskElementType));
                ilProcessor.Append(Instruction.Create(OpCodes.Call, invokeAsyncMethod));

                ilProcessor.Append(Instruction.Create(OpCodes.Ret));

                //proxyMethod.Overrides.Add(method);

                proxyType.Methods.Add(proxyMethod);
            }

            module.Types.Add(proxyType);

            return true;
        }

        public bool Inject(string targetAssemblyPath, string? injectedAssemblyPath = null)
        {
            this.assemblyResolver.AddSearchDirectory(
                Path.GetDirectoryName(targetAssemblyPath));

            var targetAssemblyName = Path.GetFileNameWithoutExtension(
                targetAssemblyPath);

            using (var targetAssembly = AssemblyDefinition.ReadAssembly(
                targetAssemblyPath,
                new ReaderParameters(ReadingMode.Immediate)
                {
                    ReadSymbols = true,
                    ReadWrite = false,
                    InMemory = true,
                    AssemblyResolver = this.assemblyResolver,
                }))
            {
                var targetTypes = targetAssembly.MainModule.GetTypes().
                    Where(td => td.IsInterface && td.Interfaces.Any(ii => ii.InterfaceType.FullName == this.iHostType.FullName)).
                    ToArray();

                if (targetTypes.Length >= 1)
                {
                    var injected = false;

                    foreach (var targetType in targetTypes)
                    {
                        if (this.InjectIntoType(targetAssembly.MainModule, targetType))
                        {
                            injected = true;
                            this.message(
                                LogLevels.Trace,
                                $"Injected a static proxy: Assembly={targetAssemblyName}, Target={targetType.FullName}");
                        }
                        else
                        {
                            this.message(
                                LogLevels.Trace,
                                $"Ignored a type: Assembly={targetAssemblyName}, Target={targetType.FullName}");
                        }
                    }

                    if (injected)
                    {
                        injectedAssemblyPath = injectedAssemblyPath ?? targetAssemblyPath;
                        var tempPath = injectedAssemblyPath + ".tmp";

                        try
                        {
                            targetAssembly.Write(
                                tempPath,
                                new WriterParameters
                                {
                                    WriteSymbols = true,
                                    DeterministicMvid = true,
                                });
                            File.Delete(injectedAssemblyPath);
                            File.Move(tempPath, injectedAssemblyPath);
                        }
                        catch
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch
                            {
                            }
                            throw;
                        }
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
