using System;
using System.Threading;
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
        bool IsActive { get; }

        /// <summary>Builds the state scope. Throws if the scope can't be built.</summary>
        /// <param name="payload">Optional data registered in the state scope by its runtime type</param>
        UniTask Enter(object payload = null);

        /// <summary>Runs the entry point of the state. Cancelled when the state exits.</summary>
        UniTask Start();

        UniTask Exit();
    }


    public class DefaultState<T> : IABaseState where T : ABaseEntryPoint
    {
        protected readonly ISceneManager _sceneManager;
        protected readonly IContainerHolder _containerHolder;
        protected readonly LifetimeScope _currentScope;
        protected readonly IScopesHolderService _scopesHolderService;
        protected LifetimeScope NewScope;
        protected ScopedSceneManager _scopedSceneManager;
        protected CancellationTokenSource _lifetimeCts;
        protected T _entryPoint;
        protected Type _currentType = typeof(T);

        public Type CurrentType => _currentType;
        public bool IsActive => NewScope != null;

        [Inject]
        public DefaultState(IObjectResolver objectResolver)
        {
            _sceneManager = objectResolver.Resolve<ISceneManager>();
            _currentScope = objectResolver.Resolve<LifetimeScope>();
            _containerHolder = objectResolver.Resolve<IContainerHolder>();
            _scopesHolderService = objectResolver.Resolve<IScopesHolderService>();
        }

        public virtual UniTask Enter(object payload = null)
        {
            if (IsActive)
                throw new InvalidOperationException($"{_currentType.Name} is already active");

            Type type = typeof(T);
            _lifetimeCts = new CancellationTokenSource();
            _scopedSceneManager = new ScopedSceneManager(_sceneManager, _lifetimeCts.Token);
            NewScope = _currentScope.CreateChild<LifetimeScope>(builder =>
            {
                // Registered before installers: an installer providing a default instance of the same type
                // uses RegisterDefaultInstance, which skips it (VContainer forbids two singletons of one type)
                if (payload != null)
                    builder.RegisterInstance(payload, payload.GetType());
                foreach (var installer in _scopesHolderService.GetServices(type.Name))
                {
                    installer.Install(builder);
                }
                // Scenes loaded inside this scope are released only with this scope
                builder.RegisterInstance<ISceneManager>(_scopedSceneManager);
                // The state starts the entry point itself, so the start can be awaited and cancelled
                builder.RegisterEntryPoint<T>().WithParameter("Name", type.Name).AsSelf();
                builder.RegisterBuildCallback(_ => VooLog.Info($"{type.Name} scope created"));
            }, type.Name);

            // VContainer logs build exceptions from Awake and leaves the scope without container
            if (NewScope.Container == null)
            {
                UnityEngine.Object.Destroy(NewScope.gameObject);
                NewScope = null;
                _lifetimeCts.Cancel();
                throw new InvalidOperationException($"Failed to build {type.Name} scope, see previous errors");
            }

            _scopedSceneManager.SetResolver(NewScope.Container);
            _containerHolder.PushResolver(NewScope.Container);
            _entryPoint = NewScope.Container.Resolve<T>();
            return UniTask.CompletedTask;
        }

        public virtual UniTask Start()
        {
            if (_entryPoint == null)
                throw new InvalidOperationException($"{_currentType.Name} is not entered");
            return _entryPoint.StartAsync(_lifetimeCts.Token);
        }

        public virtual async UniTask Exit()
        {
            if (NewScope == null) return;

            var scope = NewScope;
            NewScope = null;
            _entryPoint = null;
            // Stops the entry point start and scene loads that are still in progress
            _lifetimeCts.Cancel();
            _containerHolder.RemoveResolver(scope.Container);
            // Disposes the entry point and every disposable service of the scope
            scope.Dispose();
            await _scopedSceneManager.ReleaseAllScenes();
        }
    }
}

#endif
