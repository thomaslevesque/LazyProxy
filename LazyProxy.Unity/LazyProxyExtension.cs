using System;
using Microsoft.Practices.ObjectBuilder2;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.ObjectBuilder;

namespace LazyProxy.Unity
{
    public class LazyProxyExtension : UnityContainerExtension
    {
        protected override void Initialize()
        {
            Context.Strategies.Add(new LazyProxyBuilderStrategy(Container), UnityBuildStage.PreCreation);
        }

        private class LazyProxyBuilderStrategy : BuilderStrategy
        {
            private readonly IUnityContainer _container;

            public LazyProxyBuilderStrategy(IUnityContainer container)
            {
                _container = container;
            }

            public override void PreBuildUp(IBuilderContext context)
            {
                if (context.Existing == null)
                {
                    var policy = context.Policies.Get<ILazyProxyPolicy>(context.OriginalBuildKey);
                    if (policy != null)
                    {
                        var lazyType = typeof(Lazy<>).MakeGenericType(policy.ImplementationType);
                        var lazy = _container.Resolve(lazyType);
                        var proxy = LazyProxyGenerator.CreateProxy(policy.ServiceType, policy.ImplementationType, lazy);
                        context.Existing = proxy;
                    }
                }
                base.PreBuildUp(context);
            }
        }
    }
}
