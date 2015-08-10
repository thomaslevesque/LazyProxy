using System;
using System.Reflection;
using System.Reflection.Emit;
using FluentIL;

namespace LazyProxy
{
    public static class LazyProxyGenerator
    {
        public static Type GetLazyProxyType<TFrom, TTo>()
        {
            return TypeCache<TFrom, TTo>.LazyProxyType;
        }

        #region Lazy proxy type generation

        private static readonly ModuleBuilder _module;

        static LazyProxyGenerator()
        {
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DynamicLazyProxies"), AssemblyBuilderAccess.Run);
            _module = assembly.DefineDynamicModule("DynamicLazyProxies");
        }

        static class TypeCache<TFrom, TTo>
        {
            // ReSharper disable once StaticMemberInGenericType (intentional)
            internal static readonly Type LazyProxyType;

            static TypeCache()
            {
                LazyProxyType = TypeCache<TFrom>.OpenLazyProxyType.MakeGenericType(typeof(TTo));
            }
        }

        static class TypeCache<TFrom>
        {
            // ReSharper disable once StaticMemberInGenericType (intentional)
            internal static readonly Type OpenLazyProxyType;

            static TypeCache()
            {
                OpenLazyProxyType = CreateOpenLazyProxyType<TFrom>();
            }
        }

        private static Type CreateOpenLazyProxyType<TFrom>()
        {
            string typeName = typeof(TFrom).FullName.Replace('.', '_') + "_LazyProxy";
            var type = _module.DefineType(typeName);
            type.AddInterfaceImplementation(typeof(TFrom));
            var typeParams = type.DefineGenericParameters("TTo");
            var tto = typeParams[0];
            tto.SetInterfaceConstraints(typeof(TFrom));
            var lazyType = typeof(Lazy<>).MakeGenericType(tto);
            var lazyField = type.DefineField("_lazy", lazyType, FieldAttributes.InitOnly | FieldAttributes.Private);

            CreateConstructor(type, lazyField);

            var lazyValueGetter = typeof(Lazy<TFrom>).GetProperty("Value").GetGetMethod();
            foreach (var member in typeof(TFrom).GetMembers())
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Method:
                        CreateMethod(type, (MethodInfo)member, lazyField, lazyValueGetter);
                        break;
                    case MemberTypes.Property:
                        break;
                    case MemberTypes.Event:
                        break;
                }
            }
            return type.CreateType();
        }

        private static void CreateConstructor(TypeBuilder typeBuilder, FieldBuilder lazyField)
        {
            var ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { lazyField.FieldType });
            ctor.GetILGenerator().Fluent()
                .Ldarg_0().Call(typeof(object).GetConstructor(Type.EmptyTypes)) // : base()
                .Ldarg_0().Ldarg_1().Stfld(lazyField) // this._lazy = lazy
                .Ret();
        }

        private static void CreateMethod(TypeBuilder typeBuilder, MethodInfo targetMethod, FieldBuilder lazyField, MethodInfo lazyValueGetter)
        {
            var parameters = targetMethod.GetParameters();
            var paramTypes = Array.ConvertAll(parameters, p => p.ParameterType);
            var method = typeBuilder.DefineMethod(
                targetMethod.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                targetMethod.ReturnType,
                paramTypes);
            var il = method.GetILGenerator().Fluent();
            il.Ldarg_0().Ldfld(lazyField) // this._lazy
              .Callvirt(lazyValueGetter); // .Value
            for (uint i = 0; i < parameters.Length; i++)
            {
                il.Ldarg(i);
            }
            il.Callvirt(targetMethod);
            il.Ret();
        }

        #endregion
    }
}
