using System.Collections.Generic;
using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "Composite installer", menuName = "SO/Composite Installer")]
    public class CompositeInstaller : ScriptableInstaller
    {
        [SerializeField] public List<ScriptableInstaller> _installers = new();

        public override void Install(IContainerBuilder builder)
        {
            foreach (var installer in _installers)
            {
                installer.Install(builder);
            }
        }
    }
}
#endif