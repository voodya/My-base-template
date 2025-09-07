using System;
using System.Collections.Generic;
using VContainer;

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
            if (_nawInstallersContainer.ContainsKey(scope))
                return _nawInstallersContainer[scope]._installers;
            else
            {
                throw new Exception($"No register installers from {scope}");
            }
        }

    }
}





