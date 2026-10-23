using System.Collections.Generic;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    /// <summary>
    /// Service locator over the container of the latest active state.
    /// Prefer constructor injection, and [Inject] members for objects of scenes loaded via ISceneManager:
    /// they are injected automatically with the dependencies of the state that loaded the scene.
    /// </summary>
    public interface IContainerHolder
    {
        T Resolve<T>();
        void SetResolver(IObjectResolver resolver);
        void PushResolver(IObjectResolver resolver);
        void RemoveResolver(IObjectResolver resolver);
        bool TryResolve<T>(out T data);
		void Inject(object instance);
	}

    /// <summary>
    /// Holds resolvers of active state scopes as a stack. The last pushed alive scope is current;
    /// when it is removed, the previous one becomes current again (root container if none).
    /// </summary>
    [Service(typeof(ContainerHolderInstaller))]
    public class ContainerHolderService : IContainerHolder
    {
        private readonly IObjectResolver _rootResolver;
        private readonly List<IObjectResolver> _resolvers = new();

        private IObjectResolver _objectResolver => _resolvers.Count > 0 ? _resolvers[_resolvers.Count - 1] : _rootResolver;

        [Inject]
        public ContainerHolderService(IObjectResolver rootResolver)
        {
            _rootResolver = rootResolver;
        }

        /// <summary>
        /// Replaces all pushed resolvers with the given one.
        /// </summary>
        public void SetResolver(IObjectResolver resolver)
        {
            _resolvers.Clear();
            _resolvers.Add(resolver);
        }

        public void PushResolver(IObjectResolver resolver)
        {
            _resolvers.Remove(resolver);
            _resolvers.Add(resolver);
        }

        public void RemoveResolver(IObjectResolver resolver)
        {
            _resolvers.Remove(resolver);
        }

        public bool TryResolve<T>(out T data)
        {
            return _objectResolver.TryResolve<T>(out data);
        }

        public T Resolve<T>()
        {
            return _objectResolver.Resolve<T>();
        }

		public void Inject(object instance)
		{
			_objectResolver.Inject(instance);
		}
	}

}

#endif