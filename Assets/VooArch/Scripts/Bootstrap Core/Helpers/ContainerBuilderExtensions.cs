#if HAS_VOODYA
using VContainer;

namespace Voodya.Core
{
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Registers an instance only if the type is not registered yet in this scope.
        /// Use it in installers for default settings, which a state payload can override:
        /// TryChangeScope&lt;TEntryPoint&gt;(payload) registers the payload before installers run.
        /// </summary>
        /// <returns>true if the default instance was registered</returns>
        public static bool RegisterDefaultInstance<T>(this IContainerBuilder builder, T instance)
        {
            if (builder.Exists(typeof(T), includeInterfaceTypes: true))
                return false;
            builder.RegisterInstance(instance);
            return true;
        }
    }
}
#endif
