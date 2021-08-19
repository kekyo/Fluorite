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
    public static class InternalDynamicProxyGenerator
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

        private static TypeBuilder CreateProxyType(
            ModuleBuilder moduleBuilder,
            Type interfaceType,
            string targetName)
        {
            var typeBuilder = moduleBuilder.DefineType(
                targetName,
                TypeAttributes.Sealed | TypeAttributes.Class,
                typeof(DynamicProxyBase),
                new[] { interfaceType });

            typeBuilder.DefineDefaultConstructor(
                MethodAttributes.Public);

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

                ilGenerator.Emit(OpCodes.Ldstr, ProxyUtilities.GetMethodIdentity(interfaceType, method.Name));

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

            return typeBuilder;
        }

        internal static InternalDynamicProxyFactory CreateProxyFactory<TPeer>()
            where TPeer : class, IHost
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

            var proxyTypeBuilder = CreateProxyType(moduleBuilder, interfaceType, targetName);

#if NETFRAMEWORK
            var proxyType = proxyTypeBuilder.CreateType()!;
#else
            var proxyType = proxyTypeBuilder.CreateTypeInfo()!;
#endif

            var factoryType = typeof(InternalDynamicProxyFactory<,>).MakeGenericType(interfaceType, proxyType);
            var factory = (InternalDynamicProxyFactory)Activator.CreateInstance(factoryType)!;

            return factory;
        }
    }
}
