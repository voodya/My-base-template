using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ContainerHolderInstaller", menuName = "Scriptable Objects/ContainerHolderInstaller")]
    public class ContainerHolderInstaller : ScriptableInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ContainerHolderService>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }

}

#endif