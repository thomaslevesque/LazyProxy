using System;
using Microsoft.Practices.ObjectBuilder2;
using Microsoft.Practices.Unity;

namespace LazyProxy.Unity
{
    public class InjectionLazyProxy : InjectionMember
    {
        public override void AddPolicies(Type serviceType, Type implementationType, string name, IPolicyList policies)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType), "The service type cannot be null");
            if (implementationType == null)
                throw new ArgumentNullException(nameof(implementationType), "The implementation type cannot be null");
            policies.Set(
                typeof(ILazyProxyPolicy),
                new LazyProxyPolicy(serviceType, implementationType),
                new NamedTypeBuildKey(serviceType, name));
        }
    }
}