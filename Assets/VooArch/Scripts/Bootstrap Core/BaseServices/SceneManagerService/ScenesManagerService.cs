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
        UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action<T> onDispose = null) where T : ABaseScene;
        UniTask<T> GetScene<T>() where T : ABaseScene;
        UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action<T> onDispose) where T : ABaseScene;
        UniTask<T> GetSceneWithAfterAction<T>(Action<T> afterShow) where T : ABaseScene;
        UniTask<T> GetSceneWithBeforeAction<T>(Action<T> beforeShow) where T : ABaseScene;
        UniTask<T> GetSceneWithDisposeAction<T>(Action<T> onDispose) where T : ABaseScene;

        //void RegisterScene<T>(AssetReference assetReference) where T : ABaseScene;
        void RegisterScene(Type type, AssetReference assetReference);
        void RegisterScene(Type type, string assetReference);
        UniTask ReleaseAllScenes();
        UniTask ReleaseScene<T>() where T : ABaseScene;
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
        private readonly Dictionary<Type, Action<ABaseScene>> _onDisposeActions;
        private SyncAnimationController _animationController = new();


        public ScenesManagerService()
        {
            _registredScenes = new Dictionary<Type, AssetReference>();
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
            if (!_registredScenes.ContainsKey(type))
                _registredScenesStrings.Add(type, assetReference);
            else
            {
                Debug.LogError($"{type} already registred");
            }
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
                AsyncOperationHandle<SceneInstance> loadingOperation = Addressables.LoadSceneAsync(_registredScenes[screenType], LoadSceneMode.Additive);
                //AsyncOperationHandle<SceneInstance> loadingOperations = Addressables.LoadSceneAsync(_registredScenesStrings[screenType], LoadSceneMode.Additive);

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

        public async UniTask<T> GetSceneWithDisposeAction<T>(Action<T> onDispose)
            where T : ABaseScene
        {
            return await GetScene<T>(false, null, null, onDispose);
        }

        public async UniTask<T> GetSceneWithActions<T>(Action<T> beforeShow, Action<T> afterShow, Action<T> onDispose)
            where T : ABaseScene
        {
            return await GetScene<T>(false, beforeShow, afterShow, onDispose);
        }

        public async UniTask<T> GetScene<T>(bool hide = false, Action<T> beforeShow = null, Action<T> afterShow = null, Action<T> onDispose = null)
            where T : ABaseScene
        {

            Type screenType = typeof(T);
            _onDisposeActions[screenType] = (Action<ABaseScene>)onDispose;
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
                AsyncOperationHandle<SceneInstance> loadingOperation = Addressables.LoadSceneAsync(_registredScenes[screenType], LoadSceneMode.Additive);
                //AsyncOperationHandle<SceneInstance> loadingOperations = Addressables.LoadSceneAsync(_registredScenesStrings[screenType], LoadSceneMode.Additive);

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


        public async UniTask ReleaseScene<T>()
            where T : ABaseScene
        {
            Type screenType = typeof(T);

            if (_loadedScenes.ContainsKey(screenType))
            {
                _animationController.RemoveAnimation(_loadedScenes[screenType].Item1.Animation);
                await _loadedScenes[screenType].Item1.Hide();
                if (_onDisposeActions.ContainsKey(screenType))
                    _onDisposeActions[screenType]?.Invoke(_loadedScenes[screenType].Item1);
                _loadedScenes[screenType].Item1.Dispose();
                
                await Addressables.UnloadSceneAsync(_loadedScenes[screenType].Item2);
                _loadedScenes.Remove(screenType);
            }
            else
                Debug.LogError($"No instance scene {screenType}");

            ValidateScenes();

        }
    }

}

#endif