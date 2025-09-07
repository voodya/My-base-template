using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ApplicationScopesInstaller", menuName = "Scriptable Objects/ApplicationScopesInstaller")]
    public class ApplicationScopesInstaller : ScriptableInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ApplicationScopesService>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}


#endif