using UnityEngine;
using VContainer;

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

