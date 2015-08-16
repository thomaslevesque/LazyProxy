using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace LazyProxy
{
    public static class LazyProxyGenerator
    {
        public static Type GetLazyProxyType<TFrom, TTo>()
        {
            return GetLazyProxyType(typeof (TFrom), typeof (TTo));
        }

        public static Type GetLazyProxyType(Type fromType, Type toType)
        {
            var cache = GetCache(fromType, toType);
            return cache.LazyProxyType;
        }

        public static TFrom CreateProxy<TFrom, TTo>(Lazy<TTo> lazy)
            where TTo : TFrom
        {
            return (TFrom) CreateProxy(typeof (TFrom), typeof (TTo), lazy);
        }

        public static object CreateProxy(Type fromType, Type toType, object lazy)
        {
            var cache = GetCache(fromType, toType);
            return cache.LazyProxyConstructorDelegate(lazy);
        }

        #region Cache

        private static readonly ConcurrentDictionary<Type, OpenLazyProxyTypeCache> _lazyProxyTypeCache =
            new ConcurrentDictionary<Type, OpenLazyProxyTypeCache>();

        private class OpenLazyProxyTypeCache
        {
            private readonly ConcurrentDictionary<Type, LazyProxyTypeCache> _closedCache;
            private readonly Type _openLazyProxyType;

            public OpenLazyProxyTypeCache(Type fromType)
            {
                _closedCache = new ConcurrentDictionary<Type, LazyProxyTypeCache>();
                _openLazyProxyType = CreateOpenLazyProxyType(fromType);
            }

            public LazyProxyTypeCache GetCache(Type toType)
            {
                return _closedCache.GetOrAdd(toType, t => new LazyProxyTypeCache(_openLazyProxyType, t));
            }
        }
        private class LazyProxyTypeCache
        {
            public Type LazyProxyType { get; }
            public Func<object, object> LazyProxyConstructorDelegate { get; }

            public LazyProxyTypeCache(Type openLazyProxyType, Type toType)
            {
                LazyProxyType = openLazyProxyType.MakeGenericType(toType);
                var lazyType = typeof (Lazy<>).MakeGenericType(toType);
                var ctor = LazyProxyType.GetConstructor(new[] { lazyType });
                var arg = Expression.Parameter(typeof(object), "lazy");
                // ReSharper disable once AssignNullToNotNullAttribute
                var expr = Expression.Lambda<Func<object, object>>(
                    Expression.New(
                        ctor,
                        Expression.Convert(arg, lazyType)),
                    arg);
                LazyProxyConstructorDelegate = expr.Compile();
            }
        }

        private static LazyProxyTypeCache GetCache(Type fromType, Type toType)
        {
            var openTypeCache = _lazyProxyTypeCache.GetOrAdd(fromType, t => new OpenLazyProxyTypeCache(t));
            var cache = openTypeCache.GetCache(toType);
            return cache;
        }

        #endregion

        #region Lazy proxy type generation

        private static readonly ModuleBuilder _module;

        static LazyProxyGenerator()
        {
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DynamicLazyProxies"), AssemblyBuilderAccess.Run);
            _module = assembly.DefineDynamicModule("DynamicLazyProxies");
        }

        private static Type CreateOpenLazyProxyType(Type fromType)
        {
            string typeName = fromType.FullName.Replace('.', '_') + "_LazyProxy";
            var type = _module.DefineType(typeName);
            type.AddInterfaceImplementation(fromType);
            var typeParams = type.DefineGenericParameters("TTo");
            var tto = typeParams[0];
            tto.SetInterfaceConstraints(fromType);
            var lazyType = typeof(Lazy<>).MakeGenericType(tto);
            var lazyField = type.DefineField("_lazy", lazyType, FieldAttributes.InitOnly | FieldAttributes.Private);

            CreateConstructor(type, lazyField);

            var lazyValueGetter = typeof(Lazy<>).GetProperty("Value").GetGetMethod();
            foreach (var member in fromType.GetMembers())
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

        private static void CreateProperty(TypeBuilder typeBuilder, PropertyInfo targetProperty, FieldBuilder lazyField, MethodInfo lazyValueGetter)
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
        }



        #endregion
    }
}
