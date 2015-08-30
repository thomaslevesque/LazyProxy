using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.ObjectBuilder;

namespace LazyProxy.Unity
{
    public class LazyProxyExtension : UnityContainerExtension
    {
        protected override void Initialize()
        {
            Context.Strategies.AddNew<LazyProxyBuilderStrategy>(UnityBuildStage.PreCreation);
        }
    }
}
