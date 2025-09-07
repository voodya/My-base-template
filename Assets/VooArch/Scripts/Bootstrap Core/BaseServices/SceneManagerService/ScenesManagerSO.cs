using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;

#if HAS_VOODYA  
namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "ScenesManagerSO", menuName = "SO/Scene Manager")]
    public class ScenesManagerSO : ScriptableInstaller
    {
        [SerializeField] private AssetReference _loadingScene;

        public override void Install(IContainerBuilder builder)
        {
            ScenesManagerService scenesManagerService = new ScenesManagerService();

            scenesManagerService.RegisterScene<LoadingScene>(_loadingScene);

            builder.RegisterInstance(scenesManagerService).AsImplementedInterfaces();
        }
    }
}


#endif