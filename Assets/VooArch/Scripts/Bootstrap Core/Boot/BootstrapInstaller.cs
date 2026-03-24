using UnityEngine.SceneManagement;
#if HAS_VOODYA
using VContainer;
using VContainer.Unity;
#endif

#if HAS_VOODYA  
namespace Voodya.Core
{
    public class BootstrapInstaller : SceneInstaller
    {

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            if (SceneManager.GetActiveScene().name == "Bootstrap")
                builder.RegisterEntryPoint<BootstrapEntryPoint>().WithParameter("Name", typeof(BootstrapEntryPoint).Name);
        }
    }
}

#endif