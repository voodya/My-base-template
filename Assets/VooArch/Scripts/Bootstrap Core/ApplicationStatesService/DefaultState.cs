using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    public interface IABaseState
    {
        Type CurrentType { get; }

        void Enter();
        UniTask Exit();
    }


    public class DefaultState<T> : IABaseState where T : ABaseEntryPoint
    {
        protected ISceneManager _sceneManager;
        protected IContainerHolder _containerHolder;
        protected readonly LifetimeScope _currentScope;
        protected IScopesHolderService _scopesHolderService;
        protected IEnumerable<IDisposable> _disposed;
        protected LifetimeScope NewScope;
        protected ScopedSceneManager _scopedSceneManager;
        protected Type _currentType = typeof(T);
        public Type CurrentType => _currentType;

        [Inject]
        public DefaultState(IObjectResolver objectResolver)
        {
            _sceneManager = objectResolver.Resolve<ISceneManager>();
            _currentScope = objectResolver.Resolve<LifetimeScope>();
            _containerHolder = objectResolver.Resolve<IContainerHolder>();

            _scopesHolderService = objectResolver.Resolve<IScopesHolderService>();
        }

        public virtual void Enter()
        {
            _scopedSceneManager = new ScopedSceneManager(_sceneManager);
            NewScope = _currentScope.CreateChild<LifetimeScope>(builder =>
            {
                Type type = typeof(T);
                var installers = _scopesHolderService.GetServices(type.Name);
                foreach (var installer in installers)
                {
                    installer.Install(builder);
                }
                // Scenes loaded inside this scope are released only with this scope
                builder.RegisterInstance<ISceneManager>(_scopedSceneManager);
                builder.RegisterEntryPoint<T>().WithParameter("Name", type.Name);
                builder.RegisterBuildCallback(c => Debug.Log($"{type.Name} scope created"));
            });
            // VContainer logs build exceptions from Awake and leaves the scope without container
            if (NewScope.Container == null)
            {
                UnityEngine.Object.Destroy(NewScope.gameObject);
                NewScope = null;
                throw new InvalidOperationException($"Failed to build {typeof(T).Name} scope, see previous errors");
            }
            _disposed = NewScope.Container.Resolve<IEnumerable<IDisposable>>();
            _containerHolder.PushResolver(NewScope.Container);
        }
        public async virtual UniTask Exit()
        {
            if (NewScope == null) return;
            _containerHolder.RemoveResolver(NewScope.Container);
            NewScope.Dispose();
            foreach (var scope in _disposed)
            {
                scope.Dispose();
            }

            await _scopedSceneManager.ReleaseAllScenes();
        }
    }
}

#endif