// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.
///RAW
//NewScene
//LoadingScene
///RAW
using Voodya.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "ScenesMapping", menuName = "Voodya/Scenes Mapping")]
public class AUTOGEN_SCENES_MAPPING : BaseScenesSO
{
    // Поля для привязки ассетов сцен в инспекторе
    [SerializeField] private AssetReference _newScene;
    [SerializeField] private AssetReference _loadingScene;

    public override void OnRegister(ScenesManagerService service)
    {
        service.RegisterScene(typeof(NewScene), _newScene);
        service.RegisterScene(typeof(LoadingScene), _loadingScene);
    }
}
