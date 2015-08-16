using System;

namespace LazyProxy.Unity
{
    class LazyProxyPolicy : ILazyProxyPolicy
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }

        public LazyProxyPolicy(Type serviceType, Type implementationType)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
        }
    }
}