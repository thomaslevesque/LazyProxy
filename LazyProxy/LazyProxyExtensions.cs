using Microsoft.Practices.Unity;

namespace LazyProxy
{
    static class LazyProxyExtensions
    {
        public static IUnityContainer RegisterLazy<TFrom, TTo>(
            this IUnityContainer container,
            params InjectionMember[] injectionMembers)
        {
            return container.RegisterLazy<TFrom, TTo>(null, new TransientLifetimeManager(), injectionMembers);
        }

        public static IUnityContainer RegisterLazy<TFrom, TTo>(
            this IUnityContainer container,
            LifetimeManager lifetimeManager,
            params InjectionMember[] injectionMembers)
        {
            return container.RegisterLazy<TFrom, TTo>(null, lifetimeManager, injectionMembers);
        }

        public static IUnityContainer RegisterLazy<TFrom, TTo>(
            this IUnityContainer container,
            string name,
            params InjectionMember[] injectionMembers)
        {
            return container.RegisterLazy<TFrom, TTo>(name, new TransientLifetimeManager(), injectionMembers);
        }

        public static IUnityContainer RegisterLazy<TFrom, TTo>(
            this IUnityContainer container,
            string name,
            LifetimeManager lifetimeManager,
            params InjectionMember[] injectionMembers)
        {
            return container.RegisterType(
                typeof(TFrom),
                LazyProxyGenerator.GetLazyProxyType<TFrom, TTo>(),
                name, lifetimeManager, injectionMembers);
        }
    }
}
