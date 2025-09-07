using VContainer;

namespace Voodya.Core
{
    public interface IContainerHolder
    {
        T Resolve<T>();
        void SetResolver(IObjectResolver resolver);
        bool TryResolve<T>(out T data);
    }

    [Service(typeof(ContainerHolderInstaller))]
    public class ContainerHolderService : IContainerHolder
    {
        private IObjectResolver _objectResolver;

        public void SetResolver(IObjectResolver resolver)
        {
            _objectResolver = resolver;
        }

        public bool TryResolve<T>(out T data)
        {
            return _objectResolver.TryResolve<T>(out data);
        }

        public T Resolve<T>()
        {
            return _objectResolver.Resolve<T>();
        }
    }

}

