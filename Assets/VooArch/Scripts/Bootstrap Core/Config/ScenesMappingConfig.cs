using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if HAS_VOODYA
namespace Voodya.Core
{
    /// <summary>
    /// Addressable scene for every ABaseScene type, stored as type names.
    /// New scene types are added automatically in the editor, the scene reference is set in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenesMapping", menuName = "VooArch/Scenes Mapping Config")]
    public class ScenesMappingConfig : BaseScenesSO
    {
        [Serializable]
        public class SceneEntry
        {
            public string TypeName;
            public AssetReference Scene = new();
        }

        [SerializeField] private List<SceneEntry> _scenes = new();

        public List<SceneEntry> Scenes => _scenes;

        public override void OnRegister(ScenesManagerService service)
        {
            foreach (var entry in _scenes)
            {
                var type = Type.GetType(entry.TypeName);
                if (type == null)
                {
                    VooLog.Warning($"Scene type '{entry.TypeName}' not found, entry skipped");
                    continue;
                }
                if (entry.Scene == null || string.IsNullOrEmpty(entry.Scene.AssetGUID))
                {
                    VooLog.Error($"Scene for {type.Name} is not set in {name}");
                    continue;
                }
                service.RegisterScene(type, entry.Scene);
            }
        }
    }
}
#endif
