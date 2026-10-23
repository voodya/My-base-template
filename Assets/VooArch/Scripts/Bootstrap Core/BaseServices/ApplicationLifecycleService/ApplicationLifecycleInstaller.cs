using UnityEngine;
#if HAS_VOODYA
using VContainer;
using VContainer.Unity;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ApplicationLifecycleInstaller", menuName = "Scriptable Objects/ApplicationLifecycleInstaller")]
    public class ApplicationLifecycleInstaller : ScriptableInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ApplicationLifecycleService>();
        }
    }
}
#endif
