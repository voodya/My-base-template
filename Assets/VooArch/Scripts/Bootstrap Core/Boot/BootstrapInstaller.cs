using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Voodya.Core
{
    public class BootstrapInstaller : SceneInstaller
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            if (SceneManager.GetActiveScene().name == "Bootstrap")
                builder.RegisterEntryPoint<BootstrapEntryPoint>();
        }
    }
}

