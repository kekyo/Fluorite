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
        private static readonly HashSet<string> excludeList = new (StringComparer.InvariantCultureIgnoreCase)
            {
                "Fluorite.dll",
                "Fluorite.Core.dll",
                "Fluorite.Serializer.dll",
                "Fluorite.Transport.dll",
                "Fluorite.Dynamic.dll",
            };

        private readonly string targetAssemblyPath;

        private readonly Action<LogLevels, string> message;
        private readonly DefaultAssemblyResolver assemblyResolver = new();

        private readonly AssemblyDefinition[] referenceAssemblies;

        private readonly TypeSystem typeSystem;
        private readonly TypeDefinition attributeTargetsType;
        private readonly object attributeTargetsRawValue;
        private readonly MethodDefinition attributeUsageAttributeConstructor;
        
        private readonly TypeDefinition iHostType;

        private readonly TypeDefinition generatedProxyBaseType;
        private readonly MethodDefinition generatedProxyBaseConstructor;
        private readonly MethodDefinition invokeAsyncMethodT;

        private readonly TypeDefinition generatedProxyAttributeType;
        private readonly MethodDefinition generatedProxyAttributeConstructor;
        private readonly MethodDefinition generatedProxyAttributeInitializeMethod;

        private readonly MethodDefinition registerMethodT;

        public StaticProxyGenerator(string[] referencesBasePath, string targetAssemblyPath, Action<LogLevels, string> message)
        {
            this.targetAssemblyPath = targetAssemblyPath;
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
            
            this.generatedProxyAttributeType = fluoriteCoreAssembly.MainModule.GetType(
                "Fluorite.Internal.GeneratedProxyAttribute")!;
            this.generatedProxyAttributeConstructor = this.generatedProxyAttributeType.Methods.
                First(m => m.IsConstructor);
            this.generatedProxyAttributeInitializeMethod = this.generatedProxyAttributeType.Methods.
                First(m => m.Name == "Initialize");
            
            var staticProxyFactoryType = fluoriteCoreAssembly.MainModule.GetType(
                "Fluorite.Proxy.StaticProxyFactory")!;
            this.registerMethodT = staticProxyFactoryType.Methods.
                First(m => m.Name.StartsWith("Register"));
            
            // HACK: made safer extract AttributeUsageAttribute type reference.
            var attributeUsageAttribute =
                this.generatedProxyAttributeType.CustomAttributes.First(ca =>
                    ca.AttributeType.FullName == "System.AttributeUsageAttribute");
            var attributeUsageAttributeType = attributeUsageAttribute.
                AttributeType.Resolve();
            this.attributeUsageAttributeConstructor = attributeUsageAttributeType.Methods.
                First(m => m.IsConstructor);

            this.attributeTargetsType = attributeUsageAttribute.ConstructorArguments[0].
                Type.Resolve();
            this.attributeTargetsRawValue = attributeUsageAttribute.ConstructorArguments[0].
                Value;

            this.referenceAssemblies = referencesBasePath.
                SelectMany(path => Directory.EnumerateFiles(path, "*.dll")).
                Where(path => !excludeList.Contains(Path.GetFileName(path))).
                Select(path =>
                {
                    try
                    {
                        var assembly = AssemblyDefinition.ReadAssembly(
                            path,
                            new ReaderParameters
                            {
                                InMemory = true,
                                AssemblyResolver = assemblyResolver,
                            }
                        );
                        return assembly;
                    }
                    catch
                    {
                        return null!;
                    }
                }).
                Where(assembly => assembly != null).
                ToArray();
        }

        private static string GetMethodIdentity(TypeReference type, string methodName)
        {
            var name = methodName.EndsWith("Async") ?
                methodName.Substring(0, methodName.Length - 5) :
                methodName;
            return $"{type.FullName.Replace('+', '.').Replace('/', '.')}.{name}";
        }

        private readonly struct InjectedProxy
        {
            public readonly TypeDefinition TargetType;
            public readonly TypeDefinition ProxyType;

            public InjectedProxy(TypeDefinition targetType, TypeDefinition proxyType)
            {
                this.TargetType = targetType;
                this.ProxyType = proxyType;
            }
        }

        private InjectedProxy InjectProxyType(ModuleDefinition module, TypeDefinition targetType)
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
                module.ImportReference(this.typeSystem.Void));
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
                    throw new ArgumentException(
                        $"Method doesn't have a ValueTask<T> return type: Target={targetType.FullName}.{method.Name}");
                }

                var valueTaskElementType = ((GenericInstanceType)returnType).
                    GenericArguments.FirstOrDefault();
                // TODO: support non generic ValueTask
                if (valueTaskElementType == null)
                {
                    throw new ArgumentException(
                        $"Method doesn't have a ValueTask<T> return type: Target={targetType.FullName}.{method.Name}");
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

                proxyType.Methods.Add(proxyMethod);
            }

            module.Types.Add(proxyType);

            return new InjectedProxy(targetType, proxyType);
        }

        private (TypeDefinition attributeType, MethodDefinition generatedProxyAttributeConstructor) InjectGeneratedProxyAttributeType(
            ModuleDefinition module,
            IReadOnlyList<InjectedProxy> injects,
            IReadOnlyList<MethodDefinition> initializeMethods)
        {
            var attributeType = new TypeDefinition(
                $"Fluorite.Internal.{module.Assembly.Name.Name}",
                $"GeneratedProxyAttribute",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                module.ImportReference(this.generatedProxyAttributeType));

            var attributeInitializeMethod = this.generatedProxyAttributeType.
                Methods.First(m => m.Name == "Initialize");

            //////////////////////////////////////////////
            // Apply AttributeUsageAttribute

            var attributeUsageAttribute = new CustomAttribute(
                module.ImportReference(this.attributeUsageAttributeConstructor));
            attributeUsageAttribute.ConstructorArguments.Add(
                new CustomAttributeArgument(
                    module.ImportReference(this.attributeTargetsType),
                    this.attributeTargetsRawValue));
            attributeType.CustomAttributes.Add(attributeUsageAttribute);

            //////////////////////////////////////////////
            // Default constructor

            var generatedProxyAttributeConstructor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.ImportReference(this.typeSystem.Void));
            var dcilp = generatedProxyAttributeConstructor.Body.GetILProcessor();
            dcilp.Append(Instruction.Create(OpCodes.Ldarg_0));
            dcilp.Append(Instruction.Create(OpCodes.Call,
                module.ImportReference(this.generatedProxyAttributeConstructor)));
            dcilp.Append(Instruction.Create(OpCodes.Ret));
            attributeType.Methods.Add(generatedProxyAttributeConstructor);

            //////////////////////////////////////////////
            // OnInitialize method
            
            var onInitializeMethod = new MethodDefinition(
                "OnInitialize",
                MethodAttributes.Family | MethodAttributes.Virtual,
                module.ImportReference(this.typeSystem.Void));
            
            var imilp = onInitializeMethod.Body.GetILProcessor();

            foreach (var im in initializeMethods)
            {
                imilp.Append(Instruction.Create(OpCodes.Call, im));
            }

            foreach (var injected in injects)
            {
                var registerMethod = new GenericInstanceMethod(
                    module.ImportReference(this.registerMethodT));
                registerMethod.GenericArguments.Add(
                    module.ImportReference(injected.TargetType));
                registerMethod.GenericArguments.Add(
                    module.ImportReference(injected.ProxyType));
                imilp.Append(Instruction.Create(OpCodes.Call, registerMethod));
            }

            imilp.Append(Instruction.Create(OpCodes.Ret));

            attributeType.Methods.Add(onInitializeMethod);
 
            //////////////////////////////////////////////
            // Finished
            
            module.Types.Add(attributeType);

            return (attributeType, generatedProxyAttributeConstructor);
        }

        private void InjectGeneratedProxyAttribute(AssemblyDefinition targetAssembly, TypeDefinition attributeType)
        {
            var generatedProxyAttribute = new CustomAttribute(
                targetAssembly.MainModule.ImportReference(attributeType.Methods.First(m => m.IsConstructor)));
            targetAssembly.CustomAttributes.Add(generatedProxyAttribute);
        }

        private void ReplaceInitializeMethodBody(
            ModuleDefinition module,
            MethodDefinition generatedProxyAttributeConstructor)
        {
            if (!(module.Types.FirstOrDefault(t =>
                t.IsNotPublic && t.IsAbstract && t.IsSealed &&
                (t.FullName == "Fluorite.NestInitializerExtension")) is { } nestInitializerExtensionType &&
                nestInitializerExtensionType.Methods.FirstOrDefault(m =>
                    m.IsPublic && m.IsStatic &&
                    (m.ReturnType.FullName == "System.Void") &&
                    (m.Name == "Initialize") &&
                    (m.Parameters.SingleOrDefault()?.ParameterType.FullName == "Fluorite.NestFactory")) is { } nestInitializeMethod))
            {
                throw new InvalidOperationException(
                    $"Couldn't find and replace NestInitializerExtension. Fluorite.Build script maybe didn't finish normally.");
            }

            var ilp = nestInitializeMethod.Body.GetILProcessor();
            ilp.Clear();
            ilp.Append(Instruction.Create(OpCodes.Newobj,
                module.ImportReference(generatedProxyAttributeConstructor)));
            ilp.Append(Instruction.Create(OpCodes.Call,
                module.ImportReference(this.generatedProxyAttributeInitializeMethod)));
            ilp.Append(Instruction.Create(OpCodes.Ret));
        }

        public bool Inject()
        {
            this.assemblyResolver.AddSearchDirectory(
                Path.GetDirectoryName(this.targetAssemblyPath));

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

                var initializeMethods = this.referenceAssemblies.
                    Select(referenceAssembly => referenceAssembly.CustomAttributes.
                        Select(ca => ca.AttributeType.Resolve()).
                        FirstOrDefault(at => at.BaseType.FullName == "Fluorite.Internal.GeneratedProxyAttribute")!).
                    Where(at => at != null).
                    Select(at => at.Methods.FirstOrDefault(m => m.IsPublic && m.IsStatic && (m.Name == "Initialize"))!).
                    Where(m => m != null).
                    ToArray();

                var injects = new List<InjectedProxy>();
                    
                foreach (var targetType in targetTypes)
                {
                    var injected = this.InjectProxyType(targetAssembly.MainModule, targetType);
                    injects.Add(injected);
                        
                    this.message(
                        LogLevels.Trace,
                        $"Injected a static proxy: Assembly={targetAssemblyName}, Target={targetType.FullName}");
                }

                var (attributeType, generatedProxyAttributeConstructor) = this.InjectGeneratedProxyAttributeType(
                    targetAssembly.MainModule, injects, initializeMethods);
                this.InjectGeneratedProxyAttribute(targetAssembly, attributeType);

                this.message(
                    LogLevels.Trace,
                    $"Applied a GeneratedProxyAttribute: Assembly={targetAssemblyName}");

                this.ReplaceInitializeMethodBody(
                    targetAssembly.MainModule, generatedProxyAttributeConstructor);

                this.message(
                    LogLevels.Trace,
                    $"Injected Initializer method: Assembly={targetAssemblyName}");

                var assemblyTempPath = targetAssemblyPath + ".orig";
                File.Copy(targetAssemblyPath, assemblyTempPath, true);

                var targetPdbPath = Path.Combine(
                    Path.GetDirectoryName(targetAssemblyPath)!,
                    Path.GetFileNameWithoutExtension(targetAssemblyPath)) + ".pdb";
                var pdbTempPath = targetPdbPath + ".orig";
                if (File.Exists(targetPdbPath))
                {
                    File.Copy(targetPdbPath, pdbTempPath, true);
                }
                else
                {
                    targetPdbPath = null;
                    pdbTempPath = null;
                }

                try
                {
                    targetAssembly.Write(
                        targetAssemblyPath,
                        new WriterParameters
                        {
                            WriteSymbols = true,
                            DeterministicMvid = true,
                        });

                    this.message(
                        LogLevels.Trace,
                        $"Wrote injected assembly: Assembly={targetAssemblyName}");
                }
                catch
                {
                    try
                    {
                        File.Delete(targetAssemblyPath);
                        File.Move(assemblyTempPath, targetAssemblyPath);
                        if (pdbTempPath != null)
                        {
                            File.Delete(targetPdbPath!);
                            File.Move(pdbTempPath, targetPdbPath!);
                        }
                    }
                    catch
                    {
                    }
                    throw;
                }

                File.Delete(assemblyTempPath);
                if (pdbTempPath != null)
                {
                    File.Delete(pdbTempPath);
                }
            }

            return true;
        }
    }
}
