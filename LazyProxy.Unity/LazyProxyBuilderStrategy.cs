using Microsoft.Practices.ObjectBuilder2;

namespace LazyProxy.Unity
{
    class LazyProxyBuilderStrategy : BuilderStrategy
    {
        public override void PreBuildUp(IBuilderContext context)
        {
            if (context.Existing == null)
            {
                var policy = context.Policies.Get<ILazyProxyPolicy>(context.OriginalBuildKey);
                if (policy != null)
                {
                    var lazyProxyType = LazyProxyGenerator.GetLazyProxyType(policy.ServiceType, policy.ImplementationType);
                    context.Existing = context.NewBuildUp(new NamedTypeBuildKey(lazyProxyType, context.OriginalBuildKey.Name));
                }
            }
            base.PreBuildUp(context);
        }
    }
}