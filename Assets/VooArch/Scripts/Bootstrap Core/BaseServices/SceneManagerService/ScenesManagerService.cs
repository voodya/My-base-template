using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;


namespace Voodya.Core
{
    public interface ISceneManager
    {
        UniTask<T> GetScene<T>(bool hide = false) where T : ABaseScene;
        UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action onDispose = null) where T : ABaseScene;
        UniTask<T> GetScene<T>() where T : ABaseScene;
        UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action onDispose) where T : ABaseScene;
        UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow) where T : ABaseScene;
        UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow) where T : ABaseScene;
        UniTask<T> GetSceneWithDisposeAction<T>(Action onDispose) where T : ABaseScene;

        void RegisterScene(Type type, AssetReference assetReference);
        void RegisterScene(Type type, string assetReference);
        UniTask ReleaseAllScenes();
        UniTask ReleaseScene<T>() where T : ABaseScene;
        UniTask ReleaseScene(Type type);
        bool IsLoaded(Type type);
    }

    public class SyncAnimationController
    {
        private List<ABaseUIAnimation> _animations = new();

        public bool IsBuzy => BuzyValidate();

        private bool BuzyValidate()
        {
            bool buzy = _animations.Any(x => x.IsTweened);
            return buzy;
        }

        public void AddAnimation(ABaseUIAnimation anim)
        {
            _animations.Add(anim);
        }

        public void RemoveAnimation(ABaseUIAnimation anim)
        {
            _animations.Remove(anim);
        }
    }


    [Service(typeof(ScenesManagerSO))]
    public class ScenesManagerService : ISceneManager
    {

        private readonly Dictionary<Type, AssetReference> _registredScenes;
        private readonly Dictionary<Type, string> _registredScenesStrings;
        private readonly Dictionary<Type, (ABaseScene, AsyncOperationHandle<SceneInstance>)> _loadedScenes;
        private readonly Dictionary<Type, Action> _onDisposeActions;
        private SyncAnimationController _animationController = new();


        public ScenesManagerService()
        {
            _registredScenes = new Dictionary<Type, AssetReference>();
            _registredScenesStrings = new Dictionary<Type, string>();
            _loadedScenes = new Dictionary<Type, (ABaseScene, AsyncOperationHandle<SceneInstance>)>();
            _onDisposeActions = new();
        }

        public void RegisterScene(Type type, AssetReference assetReference)
        {
            if (assetReference == null || string.IsNullOrEmpty(assetReference.AssetGUID))
            {
                VooLog.Error($"{type} AssetReference is empty!");
                return;
            }
            if (!_registredScenes.ContainsKey(type) && !_registredScenesStrings.ContainsKey(type))
                _registredScenes.Add(type, assetReference);
            else
            {
                VooLog.Error($"{type} already registred");
            }
        }

        public void RegisterScene(Type type, string assetReference)
        {
            if (string.IsNullOrEmpty(assetReference))
            {
                VooLog.Error($"{type} address is empty!");
                return;
            }
            if (!_registredScenes.ContainsKey(type) && !_registredScenesStrings.ContainsKey(type))
                _registredScenesStrings.Add(type, assetReference);
            else
            {
                VooLog.Error($"{type} already registred");
            }
        }

        public bool IsLoaded(Type type)
        {
            return _loadedScenes.ContainsKey(type);
        }

        private AsyncOperationHandle<SceneInstance> LoadSceneAsync(Type type)
        {
            if (_registredScenes.TryGetValue(type, out var assetReference))
                return Addressables.LoadSceneAsync(assetReference, LoadSceneMode.Additive);
            if (_registredScenesStrings.TryGetValue(type, out var address))
                return Addressables.LoadSceneAsync(address, LoadSceneMode.Additive);

            throw new KeyNotFoundException($"Scene {type} is not registred");
        }

        public async UniTask ReleaseAllScenes(List<Type> notReleasedScenes)
        {
            foreach (var type in _loadedScenes.Keys.ToList())
            {
                if (notReleasedScenes.Contains(type))
                    continue;
                await ReleaseScene(type);
            }
        }

        public async UniTask ReleaseAllScenes()
        {
            await ReleaseAllScenes(new List<Type> { typeof(LoadingScene) });
        }

        private void ValidateScenes()
        {
            List<Type> types = new List<Type>();
            foreach (var item in _loadedScenes)
            {
                if (item.Value.Item1 == null)
                    types.Add(item.Key);
            }
            foreach (var type in types)
            {
                _loadedScenes.Remove(type);
            }
        }

        /// <param name="hide">if true scene has created or getted in hide state</param>
        public UniTask<T> GetScene<T>(bool hide = false)
            where T : ABaseScene
        {
            return GetScene<T>(hide, null, null, null);
        }

        public UniTask<T> GetScene<T>()
            where T : ABaseScene
        {
            return GetScene<T>(false, null, null, null);
        }

        public UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow)
            where T : ABaseScene
        {
            return GetScene<T>(false, beforeShow, null, null);
        }

        public UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow)
            where T : ABaseScene
        {
            return GetScene<T>(false, null, afterShow, null);
        }

        public UniTask<T> GetSceneWithDisposeAction<T>(Action onDispose)
            where T : ABaseScene
        {
            return GetScene<T>(false, null, null, onDispose);
        }

        public UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action onDispose)
            where T : ABaseScene
        {
            return GetScene<T>(false, beforeShow, afterShow, onDispose);
        }

        public async UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action onDispose = null)
            where T : ABaseScene
        {
            Type screenType = typeof(T);
            if (onDispose != null)
                _onDisposeActions[screenType] = onDispose;

            T scene;
            if (_loadedScenes.TryGetValue(screenType, out var loaded))
            {
                scene = loaded.Item1 as T;
                beforeShow?.Invoke(scene);
            }
            else
            {
                AsyncOperationHandle<SceneInstance> loadingOperation = LoadSceneAsync(screenType);
                var instance = await loadingOperation;
                try
                {
                    scene = instance.Scene.GetRoot<T>();
                }
                catch
                {
                    // Scene without a proper root component must not stay loaded
                    await Addressables.UnloadSceneAsync(loadingOperation);
                    throw;
                }
                beforeShow?.Invoke(scene);
                _loadedScenes[screenType] = (scene, loadingOperation);
                scene.SetAnimationController(_animationController);
                _animationController.AddAnimation(scene.Animation);
            }

            if (!hide)
                await scene.Show();
            else
                scene.SetDefaultState();
            afterShow?.Invoke(scene);
            return scene;
        }

        public UniTask ReleaseScene<T>()
            where T : ABaseScene
        {
            return ReleaseScene(typeof(T));
        }

        public async UniTask ReleaseScene(Type screenType)
        {
            if (!_loadedScenes.TryGetValue(screenType, out var loaded))
            {
                VooLog.Warning($"No instance scene {screenType}");
                return;
            }

            // Removed before awaiting, so parallel release calls don't unload it twice
            _loadedScenes.Remove(screenType);
            var (scene, handle) = loaded;
            if (scene != null)
            {
                _animationController.RemoveAnimation(scene.Animation);
                await scene.Hide();
                scene.Dispose();
            }
            if (_onDisposeActions.Remove(screenType, out var onDispose))
                onDispose?.Invoke();

            await Addressables.UnloadSceneAsync(handle);
            ValidateScenes();
        }
    }

    /// <summary>
    /// ISceneManager for a single state scope. Delegates to the global manager
    /// and remembers scenes loaded by this scope, so ReleaseAllScenes
    /// unloads only them, not scenes of the parent or sibling states.
    /// Objects of loaded scenes get [Inject] dependencies from the state scope.
    /// </summary>
    public class ScopedSceneManager : ISceneManager
    {
        private readonly ISceneManager _sceneManager;
        private readonly CancellationToken _lifetime;
        private readonly List<Type> _ownedScenes = new();
        private IObjectResolver _resolver;

        public ScopedSceneManager(ISceneManager sceneManager, CancellationToken lifetime = default)
        {
            _sceneManager = sceneManager;
            _lifetime = lifetime;
        }

        public void SetResolver(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private async UniTask<T> Get<T>(bool hide, Action<T> beforeShow, Action<T> afterShow, Action onDispose)
            where T : ABaseScene
        {
            _lifetime.ThrowIfCancellationRequested();
            Type type = typeof(T);
            bool isOwner = !_sceneManager.IsLoaded(type);

            T scene = await _sceneManager.GetScene<T>(hide, s =>
            {
                if (_lifetime.IsCancellationRequested) return;
                if (isOwner) InjectScene(s);
                beforeShow?.Invoke(s);
            }, afterShow, onDispose);

            if (isOwner && !_ownedScenes.Contains(type))
                _ownedScenes.Add(type);

            if (_lifetime.IsCancellationRequested)
            {
                // The state exited while the scene was loading: release it instead of leaking
                if (isOwner)
                    await ReleaseScene(type);
                throw new OperationCanceledException(_lifetime);
            }
            return scene;
        }

        private void InjectScene(ABaseScene scene)
        {
            if (_resolver == null) return;
            foreach (var root in scene.gameObject.scene.GetRootGameObjects())
                _resolver.InjectGameObject(root);
        }

        public UniTask<T> GetScene<T>(bool hide = false) where T : ABaseScene
            => Get<T>(hide, null, null, null);

        public UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action onDispose = null) where T : ABaseScene
            => Get(hide, beforeShow, afterShow, onDispose);

        public UniTask<T> GetScene<T>() where T : ABaseScene
            => Get<T>(false, null, null, null);

        public UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action onDispose) where T : ABaseScene
            => Get(false, beforeShow, afterShow, onDispose);

        public UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow) where T : ABaseScene
            => Get(false, null, afterShow, null);

        public UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow) where T : ABaseScene
            => Get(false, beforeShow, null, null);

        public UniTask<T> GetSceneWithDisposeAction<T>(Action onDispose) where T : ABaseScene
            => Get<T>(false, null, null, onDispose);

        public void RegisterScene(Type type, AssetReference assetReference)
            => _sceneManager.RegisterScene(type, assetReference);

        public void RegisterScene(Type type, string assetReference)
            => _sceneManager.RegisterScene(type, assetReference);

        public bool IsLoaded(Type type) => _sceneManager.IsLoaded(type);

        public UniTask ReleaseScene<T>() where T : ABaseScene
            => ReleaseScene(typeof(T));

        public async UniTask ReleaseScene(Type type)
        {
            _ownedScenes.Remove(type);
            await _sceneManager.ReleaseScene(type);
        }

        public async UniTask ReleaseAllScenes()
        {
            var scenes = _ownedScenes.ToList();
            _ownedScenes.Clear();
            foreach (var type in scenes)
            {
                if (_sceneManager.IsLoaded(type))
                    await _sceneManager.ReleaseScene(type);
            }
        }
    }

}

#endif
