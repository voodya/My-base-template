using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public class SceneInstaller : LifetimeScope
    {
        [SerializeField] public List<ScriptableInstaller> _installers;

        protected override void Configure(IContainerBuilder builder)
        {
            foreach (var installer in _installers)
            {
                installer.Install(builder);
            }
        }

    }
}

#endif