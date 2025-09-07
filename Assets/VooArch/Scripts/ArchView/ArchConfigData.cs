using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ArchConfigData", menuName = "Scriptable Objects/ArchConfigData")]
    public class ArchConfigData : ScriptableInstaller
    {
        [SerializeField] public CustomDictionary<string, ScriptableInstaller> _serviceInstallers;
        [SerializeField] public CustomDictionary<string, List<string>> _stringsData;
        [SerializeField] public CustomDictionary<string, CompositeInstaller> _compositeInstallers;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ScopesInstallersHolderService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .WithParameter("installers", _compositeInstallers.GetRawDictionary());
            foreach (var installer in _compositeInstallers["BootstrapEntryPoint"]._installers)
            {
                installer.Install(builder);
            }
        }
    }
    [System.Serializable]
    public class ServiceData
    {
        [SerializeField] public Type ServiceType;
        [SerializeField] public Type InstallerType;

        public string ServiceName => ServiceType.Name;
        public string InstallerName => InstallerType != null ? InstallerType.Name : "No installer";

        public ServiceData(Type serviceType, Type installerType)
        {
            ServiceType = serviceType;
            InstallerType = installerType;
        }
    }
}


#endif