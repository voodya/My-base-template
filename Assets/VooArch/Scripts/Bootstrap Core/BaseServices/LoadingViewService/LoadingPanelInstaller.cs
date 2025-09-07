using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA  
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

#endif
