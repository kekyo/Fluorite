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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Fluorite.Internal
{
    public static class DynamicProxyGenerator
    {
        private static readonly MethodInfo invokeAsyncMethod =
            typeof(DynamicProxyBase).GetMethod(
                "InvokeAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly ConstructorInfo peerProxyBaseConstructor =
            typeof(DynamicProxyBase).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(Nest) },
                null)!;

        private struct Proxy
        {
            public readonly TypeBuilder Type;
            public readonly ConstructorBuilder Constructor;

            public Proxy(TypeBuilder type, ConstructorBuilder constructor)
            {
                this.Type = type;
                this.Constructor = constructor;
            }
        }

        private static Proxy CreateProxyType(
            ModuleBuilder moduleBuilder,
            Type interfaceType,
            string targetName)
        {
            var typeBuilder = moduleBuilder.DefineType(
                targetName,
                TypeAttributes.Sealed | TypeAttributes.Class,
                typeof(DynamicProxyBase),
                new[] { interfaceType });

            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                default,
                new[] { typeof(Nest) });

            var constructorIlGenerator = constructorBuilder.GetILGenerator();
            constructorIlGenerator.Emit(OpCodes.Ldarg_0);
            constructorIlGenerator.Emit(OpCodes.Ldarg_1);
            constructorIlGenerator.Emit(OpCodes.Call, peerProxyBaseConstructor);
            constructorIlGenerator.Emit(OpCodes.Ret);

            foreach (var method in interfaceType.GetMethods())
            {
                if (method.IsGenericMethod)
                {
                    throw new ArgumentException(
                        $"Couldn't generate a proxy from generic method: {method.DeclaringType?.FullName ?? "global"}.{method.Name}");
                }

                if (!method.ReturnType.FullName?.StartsWith("System.Threading.Tasks.ValueTask") ?? false)
                {
                    throw new ArgumentException(
                        $"Couldn't generate a proxy from non ValueTask returned method: {method.DeclaringType?.FullName ?? "global"}.{method.Name}");
                }

                var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                var methodBuilder = typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    method.ReturnType,
                    parameterTypes);

                var ilGenerator = methodBuilder.GetILGenerator();

                ilGenerator.Emit(OpCodes.Ldarg_0);

                ilGenerator.Emit(OpCodes.Ldstr, $"{interfaceType.FullName}.{method.Name}");

                ilGenerator.Emit(OpCodes.Ldc_I4_S, (short)parameterTypes.Length);
                ilGenerator.Emit(OpCodes.Newarr, typeof(object));

                for (short index = 0; index < parameterTypes.Length; index++)
                {
                    var parameterType = parameterTypes[index];

                    ilGenerator.Emit(OpCodes.Dup);
                    ilGenerator.Emit(OpCodes.Ldc_I4_S, index);
                    ilGenerator.Emit(OpCodes.Conv_I);
                    ilGenerator.Emit(OpCodes.Ldarg_S, (short)(index + 1));  // Skip this ref.
                    if (parameterType.IsValueType)
                    {
                        ilGenerator.Emit(OpCodes.Box, parameterType);
                    }
                    ilGenerator.Emit(OpCodes.Stelem_Ref);
                }

                var valueTaskElementType = method.ReturnType.GenericTypeArguments[0];
                var madeInternalInvokeMethod =
                    invokeAsyncMethod.MakeGenericMethod(valueTaskElementType);
                ilGenerator.Emit(OpCodes.Call, madeInternalInvokeMethod);

                ilGenerator.Emit(OpCodes.Ret);

                typeBuilder.DefineMethodOverride(methodBuilder, method);
            }

            return new Proxy(typeBuilder, constructorBuilder);
        }

        private static TypeBuilder CreateFactoryType(
            ModuleBuilder moduleBuilder,
            Proxy proxy,
            string targetName)
        {
            var typeBuilder = moduleBuilder.DefineType(
                targetName + "Factory",
                TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class);

            var methodBuilder = typeBuilder.DefineMethod(
                "CreateInstance",
                MethodAttributes.Public | MethodAttributes.Static,
                proxy.Type,
                new[] { typeof(Nest) });

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Newobj, proxy.Constructor);
            ilGenerator.Emit(OpCodes.Ret);

            return typeBuilder;
        }

        internal static Func<Nest, TPeer> CreateProxyFactory<TPeer>()
        {
            var interfaceType = typeof(TPeer);
            var targetName = interfaceType.Name;
            var exactTargetName = $"{targetName}_{Guid.NewGuid():N}";
            var assenblyName = new AssemblyName(exactTargetName);
#if NETFRAMEWORK
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assenblyName, AssemblyBuilderAccess.Run);
#else
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assenblyName, AssemblyBuilderAccess.Run);
#endif
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(exactTargetName);

            var proxy = CreateProxyType(moduleBuilder, interfaceType, targetName);
            var factoryTypeBuilder = CreateFactoryType(moduleBuilder, proxy, targetName);

#if NETFRAMEWORK
            var proxyType = proxy.Type.CreateType()!;
            var factoryType = factoryTypeBuilder.CreateType()!;
#else
            var proxyType = proxy.Type.CreateTypeInfo()!;
            var factoryType = factoryTypeBuilder.CreateTypeInfo()!;
#endif

            var factoryMethod = factoryType.GetMethod("CreateInstance")!;

            return (Func<Nest, TPeer>)Delegate.CreateDelegate(
                typeof(Func<Nest, TPeer>),
                factoryMethod);
        }
    }
}
