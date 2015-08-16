using System;
using Microsoft.Practices.ObjectBuilder2;

namespace LazyProxy.Unity
{
    interface ILazyProxyPolicy : IBuilderPolicy
    {
        Type ServiceType { get; }
        Type ImplementationType { get; }
    }
}