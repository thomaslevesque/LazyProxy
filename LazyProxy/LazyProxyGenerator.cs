using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace LazyProxy
{
    public static class LazyProxyGenerator
    {
        public static Type GetLazyProxyType<TFrom, TTo>()
        {
            return TypeCache<TFrom, TTo>.LazyProxyType;
        }

        public static TFrom CreateProxy<TFrom, TTo>(Lazy<TTo> lazy)
            where TTo : TFrom
        {
            return TypeCache<TFrom, TTo>.LazyProxyConstructorDelegate(lazy);
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
            internal static readonly Func<Lazy<TTo>, TFrom> LazyProxyConstructorDelegate;

            static TypeCache()
            {
                LazyProxyType = TypeCache<TFrom>.OpenLazyProxyType.MakeGenericType(typeof(TTo));
                var ctor = LazyProxyType.GetConstructor(new[] {typeof (Lazy<TTo>)});
                var arg = Expression.Parameter(typeof (Lazy<TTo>), "lazy");
                // ReSharper disable once AssignNullToNotNullAttribute
                var expr = Expression.Lambda<Func<Lazy<TTo>, TFrom>>(Expression.New(ctor, arg), arg);
                LazyProxyConstructorDelegate = expr.Compile();
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
                        var method = (MethodInfo) member;
                        if (method.IsSpecialName)
                            continue;
                        CreateMethod(type, method, lazyField, lazyValueGetter);
                        break;
                    case MemberTypes.Property:
                        CreateProperty(type, (PropertyInfo) member, lazyField, lazyValueGetter);
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
            ctor.DefineParameter(1, ParameterAttributes.None, "lazy");
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            // ReSharper disable once AssignNullToNotNullAttribute (I know this ctor exists...)
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, lazyField);
            il.Emit(OpCodes.Ret);
        }

        private static MethodBuilder CreateMethod(TypeBuilder typeBuilder, MethodInfo targetMethod, FieldBuilder lazyField, MethodInfo lazyValueGetter)
        {
            var parameters = targetMethod.GetParameters();
            var paramTypes = Array.ConvertAll(parameters, p => p.ParameterType);
            var method = typeBuilder.DefineMethod(
                targetMethod.Name,
                (targetMethod.Attributes | MethodAttributes.Final) & ~MethodAttributes.Abstract,
                targetMethod.ReturnType,
                paramTypes);
            foreach(var param in parameters)
            {
                method.DefineParameter(param.Position + 1, param.Attributes, param.Name);
            }

            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, lazyField);
            il.Emit(OpCodes.Callvirt, lazyValueGetter);
            for (short i = 1; i <= parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i);
            }
            il.Emit(OpCodes.Callvirt, targetMethod);
            il.Emit(OpCodes.Ret);
            return method;
        }

        private static PropertyBuilder CreateProperty(TypeBuilder typeBuilder, PropertyInfo targetProperty, FieldBuilder lazyField, MethodInfo lazyValueGetter)
        {
            var parameters = targetProperty.GetIndexParameters();
            var paramTypes = Array.ConvertAll(parameters, p => p.ParameterType);
            var property = typeBuilder.DefineProperty(
                targetProperty.Name,
                targetProperty.Attributes,
                targetProperty.PropertyType,
                paramTypes);

            if (targetProperty.CanRead)
            {
                var getter = CreateMethod(typeBuilder, targetProperty.GetMethod, lazyField, lazyValueGetter);
                property.SetGetMethod(getter);
            }
            if (targetProperty.CanWrite)
            {
                var setter = CreateMethod(typeBuilder, targetProperty.SetMethod, lazyField, lazyValueGetter);
                property.SetSetMethod(setter);
            }
            return property;
        }



        #endregion
    }
}
