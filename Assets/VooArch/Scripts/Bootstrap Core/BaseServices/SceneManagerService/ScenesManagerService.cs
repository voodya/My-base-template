using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if HAS_VOODYA  
using Cysharp.Threading.Tasks;


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

        //void RegisterScene<T>(AssetReference assetReference) where T : ABaseScene;
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
            if (assetReference.AssetGUID == "")
            {
                Debug.LogError($"{type} AssetReference is null!");
                return;
            }
            if (!_registredScenes.ContainsKey(type))
                _registredScenes.Add(type, assetReference);
            else
            {
                Debug.LogError($"{type} already registred");
            }
        }

        public void RegisterScene(Type type, string assetReference)
        {
            if (string.IsNullOrEmpty(assetReference))
            {
                Debug.LogError($"{type} address is empty!");
                return;
            }
            if (!_registredScenes.ContainsKey(type) && !_registredScenesStrings.ContainsKey(type))
                _registredScenesStrings.Add(type, assetReference);
            else
            {
                Debug.LogError($"{type} already registred");
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
            foreach (var item in _loadedScenes)
            {
                if (notReleasedScenes.Contains(item.Key))
                    continue;
                await Addressables.UnloadSceneAsync(_loadedScenes[item.Key].Item2);
            }

            ValidateScenes();
        }

        public async UniTask ReleaseAllScenes()
        {
            foreach (var item in _loadedScenes)
            {
                if (item.Key == typeof(LoadingScene))
                    continue;
                if (_onDisposeActions.ContainsKey(item.Key))
                    _onDisposeActions[item.Key]?.Invoke();
                await Addressables.UnloadSceneAsync(_loadedScenes[item.Key].Item2);
            }
            ValidateScenes();
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hide">if true scene has created or getted in hide state</param>
        /// <returns></returns>
        public async UniTask<T> GetScene<T>(bool hide = false)
            where T : ABaseScene
        {

            Type screenType = typeof(T);
            if (_loadedScenes.ContainsKey(screenType))
            {
                T scene = _loadedScenes[screenType].Item1 as T;
                if (!hide)
                {
                    await scene.Show();
                }
                else
                    scene.SetDefaultState();
                return scene;
            }
            else
            {
                AsyncOperationHandle<SceneInstance> loadingOperation = LoadSceneAsync(screenType);

                T scene = (await loadingOperation).Scene.GetRoot<T>();
                _loadedScenes[screenType] = (scene, loadingOperation);
                scene.SetAnimationController(_animationController);
                _animationController.AddAnimation(scene.Animation);
                if (!hide)
                    await scene.Show();
                else
                    scene.SetDefaultState();
                return scene;
            }
        }

        public async UniTask<T> GetScene<T>()
            where T : ABaseScene
        {
            return await GetScene<T>(false);
        }

        public async UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow)
            where T : ABaseScene
        {
            return await GetScene<T>(false, beforeShow);
        }

        public async UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow)
            where T : ABaseScene
        {
            return await GetScene<T>(false, null, afterShow);
        }

        public async UniTask<T> GetSceneWithDisposeAction<T>(Action onDispose)
            where T : ABaseScene
        {
            return await GetScene<T>(false, null, null, onDispose);
        }

        public async UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action onDispose)
            where T : ABaseScene
        {
            return await GetScene<T>(false, beforeShow, afterShow, onDispose);
        }

        public async UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action onDispose = null)
            where T : ABaseScene
        {

            Type screenType = typeof(T);
            _onDisposeActions[screenType] = onDispose;
            if (_loadedScenes.ContainsKey(screenType))
            {
                T scene = _loadedScenes[screenType].Item1 as T;
                beforeShow?.Invoke(scene);
                if (!hide)
                {
                    await scene.Show();
                }
                else
                    scene.SetDefaultState();
                afterShow?.Invoke(scene);
                return scene;
            }
            else
            {
                AsyncOperationHandle<SceneInstance> loadingOperation = LoadSceneAsync(screenType);

                T scene = (await loadingOperation).Scene.GetRoot<T>();
                beforeShow?.Invoke(scene);
                _loadedScenes[screenType] = (scene, loadingOperation);
                scene.SetAnimationController(_animationController);
                _animationController.AddAnimation(scene.Animation);
                if (!hide)
                    await scene.Show();
                else
                    scene.SetDefaultState();
                afterShow?.Invoke(scene);
                return scene;
            }
        }

        public UniTask ReleaseScene<T>()
            where T : ABaseScene
        {
            return ReleaseScene(typeof(T));
        }

        public async UniTask ReleaseScene(Type screenType)
        {
            if (_loadedScenes.ContainsKey(screenType))
            {
                _animationController.RemoveAnimation(_loadedScenes[screenType].Item1.Animation);
                await _loadedScenes[screenType].Item1.Hide();
                _loadedScenes[screenType].Item1.Dispose();
                if (_onDisposeActions.ContainsKey(screenType))
                    _onDisposeActions[screenType]?.Invoke();

                await Addressables.UnloadSceneAsync(_loadedScenes[screenType].Item2);
                _loadedScenes.Remove(screenType);
            }
            else
                Debug.LogError($"No instance scene {screenType}");

            ValidateScenes();

        }
    }

    /// <summary>
    /// ISceneManager for a single state scope. Delegates to the global manager
    /// and remembers scenes loaded by this scope, so ReleaseAllScenes
    /// unloads only them, not scenes of the parent or sibling states.
    /// </summary>
    public class ScopedSceneManager : ISceneManager
    {
        private readonly ISceneManager _sceneManager;
        private readonly List<Type> _ownedScenes = new();

        public ScopedSceneManager(ISceneManager sceneManager)
        {
            _sceneManager = sceneManager;
        }

        private async UniTask<T> Track<T>(Func<UniTask<T>> load)
            where T : ABaseScene
        {
            Type type = typeof(T);
            bool isOwner = !_sceneManager.IsLoaded(type);
            T scene = await load();
            if (isOwner && !_ownedScenes.Contains(type))
                _ownedScenes.Add(type);
            return scene;
        }

        public UniTask<T> GetScene<T>(bool hide = false) where T : ABaseScene
            => Track(() => _sceneManager.GetScene<T>(hide));

        public UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action onDispose = null) where T : ABaseScene
            => Track(() => _sceneManager.GetScene<T>(hide, beforeShow, afterShow, onDispose));

        public UniTask<T> GetScene<T>() where T : ABaseScene
            => Track(() => _sceneManager.GetScene<T>());

        public UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action onDispose) where T : ABaseScene
            => Track(() => _sceneManager.GetSceneWithActions<T>(beforeShow, afterShow, onDispose));

        public UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow) where T : ABaseScene
            => Track(() => _sceneManager.GetSceneWithAfterAction<T>(afterShow));

        public UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow) where T : ABaseScene
            => Track(() => _sceneManager.GetSceneWithBeforeAction<T>(beforeShow));

        public UniTask<T> GetSceneWithDisposeAction<T>(Action onDispose) where T : ABaseScene
            => Track(() => _sceneManager.GetSceneWithDisposeAction<T>(onDispose));

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