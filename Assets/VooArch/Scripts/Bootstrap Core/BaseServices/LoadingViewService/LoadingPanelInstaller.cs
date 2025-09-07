using UnityEngine;
using VContainer;

namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "LoadingPanelInstaller", menuName = "Scriptable Objects/LoadingPanelInstaller")]
    public class LoadingPanelInstaller : ScriptableInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<LoadingPanelService>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}


