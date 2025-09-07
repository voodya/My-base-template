using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
            NewScope = _currentScope.CreateChild<LifetimeScope>(builder =>
            {
                Type type = typeof(T);
                var installers = _scopesHolderService.GetServices(type.Name);
                foreach (var installer in installers)
                {
                    installer.Install(builder);
                }
                builder.RegisterEntryPoint<T>();
                builder.RegisterBuildCallback(c => Debug.Log($"{type.Name} scope created"));
            });
            _disposed = NewScope.Container.Resolve<IEnumerable<IDisposable>>();
            _containerHolder.SetResolver(NewScope.Container);
        }
        public async virtual UniTask Exit()
        {
            NewScope.Dispose();
            foreach (var scope in _disposed)
            {
                scope.Dispose();
            }

            await _sceneManager.ReleaseAllScenes();
        }
    }
}

