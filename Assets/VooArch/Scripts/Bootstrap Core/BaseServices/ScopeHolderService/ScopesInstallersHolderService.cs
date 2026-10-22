using System.Collections.Generic;
using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    public interface IScopesHolderService
    {
        List<ScriptableInstaller> GetServices(string scope);
    }

    public class ScopesInstallersHolderService : IScopesHolderService
    {
        private Dictionary<string, CompositeInstaller> _nawInstallersContainer;


        [Inject]
        public ScopesInstallersHolderService(Dictionary<string, CompositeInstaller> installers)
        {
            _nawInstallersContainer = installers;
        }

        public List<ScriptableInstaller> GetServices(string scope)
        {
            if (_nawInstallersContainer.TryGetValue(scope, out var composite) && composite != null)
                return composite._installers;

            // Entry point without own services is valid, but usually means ArchConfigData is out of date
            Debug.LogWarning($"No composite installer for {scope} in ArchConfigData, scope will be created without services. Open VooArch/Voo arch configure to update config.");
            return new List<ScriptableInstaller>();
        }

    }
}





#endif