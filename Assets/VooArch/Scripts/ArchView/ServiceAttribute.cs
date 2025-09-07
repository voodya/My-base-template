using System;

#if HAS_VOODYA  
namespace Voodya.Core
{

    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        public Type InstallerType { get; }

        public ServiceAttribute(Type installerType = null)
        {
            if (installerType != null)
            {
                if (!typeof(ScriptableInstaller).IsAssignableFrom(installerType) || installerType.IsAbstract)
                {
                    throw new ArgumentException(
                        $"Тип '{installerType.Name}' должен быть неабстрактным классом, унаследованным от {nameof(ScriptableInstaller)}.",
                        nameof(installerType));
                }
            }
            InstallerType = installerType;
        }
    }
}

#endif