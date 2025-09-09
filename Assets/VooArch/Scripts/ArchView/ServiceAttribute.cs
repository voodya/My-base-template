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

    [AttributeUsage(AttributeTargets.Class)]
    public class EntryPointAttribute : Attribute
    {
        public int Priority { get; }
        public bool Ignore { get; }

        public EntryPointAttribute(int priority = 100)
        {
            Priority = priority;
            Ignore = false;
        }

        public EntryPointAttribute(bool ignore)
        {
            Ignore = ignore;
            Priority = 100;
        }
    }
}

#endif