using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ScenesManagerSO", menuName = "SO/Scene Manager")]
    public class ScenesManagerSO : ScriptableInstaller
    {
        [SerializeField] private BaseScenesSO _scenes;
        public override void Install(IContainerBuilder builder)
        {
            ScenesManagerService scenesManagerService = new ScenesManagerService();
            _scenes.OnRegister(scenesManagerService);
            //foreach (var sceneName in _sceneNames.GetRawDictionary())
            //{
            //
            //    scenesManagerService.RegisterScene(sceneName.Key.GetType(), sceneName.Value);
            //}

            builder.RegisterInstance(scenesManagerService).AsImplementedInterfaces();
        }
    }

    public abstract class BaseScenesSO : ScriptableObject
    {
        public abstract void OnRegister(ScenesManagerService service);
    }

}


#endif