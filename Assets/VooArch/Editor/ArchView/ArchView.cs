using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if HAS_VOODYA
using Voodya.Core;
#endif
#if !HAS_VOODYA
[InitializeOnLoad]
public static class VooResolver
{
    static VooResolver()
    {
        // This code runs when the editor loads.
        // You can add logic here to check if your specific Git package is installed
        // and then call your desired method.
        Debug.Log("[VooArch] Editor loaded. Checking for Git package...");
        Resolve();
    }

    private static void Resolve()
    {

        string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        if (File.Exists(manifestPath))
        {
            try
            {
                string manifestContent = File.ReadAllText(manifestPath);
                Depend dep = JsonConvert.DeserializeObject<Depend>(manifestContent);
                if (!dep.dependencies.ContainsKey("jp.hadashikick.vcontainer"))
                    dep.dependencies["jp.hadashikick.vcontainer"] = "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer";
                else
                    Debug.Log("[VooArch] VContainer exist");
                if (!dep.dependencies.ContainsKey("com.cysharp.unitask"))
                    dep.dependencies["com.cysharp.unitask"] = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
                else
                    Debug.Log("[VooArch] UniTask exist");

                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(dep));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VooArch] Error reading manifest.json: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[VooArch] manifest.json not found at: " + manifestPath);
        }

        VooHelpers.AddNewScriptingDefineSymbols("HAS_VOODYA");


    }

}
public class Depend
{
    public Dictionary<string, string> dependencies;
}
#endif
public static class VooHelpers
{
    public static void AddNewScriptingDefineSymbols(string define)
    {
        List<BuildTargetGroup> groups = Enum.GetValues(typeof(BuildTargetGroup)).Cast<BuildTargetGroup>().ToList();

        foreach (var group in groups)
        {
            try
            {
                NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(group);
                string symb = PlayerSettings.GetScriptingDefineSymbols(target);
                if (!symb.Split(";").ToList().Any(x => x.Equals(define)))
                {
                    symb += $";{define}";
                    PlayerSettings.SetScriptingDefineSymbols(target, symb);
                }
            }
            catch (Exception)
            {
                // Obsolete or unsupported build target group
            }

        }
    }

}
#if HAS_VOODYA

/// <summary>
/// Keeps EntryPointsConfig and ScenesMappingConfig in sync with the code after every script reload.
/// Configs store type names, so adding, removing or renaming a class never breaks compilation.
/// </summary>
[InitializeOnLoad]
public static class VooConfigSync
{
    public const string ROOT_PATH = "Assets/ARCH_CONFIGURE";
    private const string ENTRY_POINTS_ASSET = ROOT_PATH + "/EntrysMapping.asset";
    private const string SCENES_ASSET = ROOT_PATH + "/ScenesMapping.asset";

    static VooConfigSync()
    {
        // Asset database is not ready during domain reload
        EditorApplication.delayCall += () => Sync();
    }

    /// <returns>true if any config has changed</returns>
    public static bool Sync()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        bool changed = SyncEntryPoints() | SyncScenes();
        if (changed)
            AssetDatabase.SaveAssets();
        return changed;
    }

    /// <summary>Name used by configs, resolvable at runtime with Type.GetType</summary>
    public static string TypeKey(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    private static readonly Dictionary<Assembly, bool> _testAssemblies = new();

    [Serializable]
    private class AssemblyDefinition
    {
        public string[] optionalUnityReferences;
        public string[] defineConstraints;
    }

    /// <summary>
    /// Types from test assemblies (fake entry points, scenes or services of unit tests)
    /// are loaded in the editor too, but are not part of the app configuration.
    /// </summary>
    public static bool IsTestType(Type type)
    {
        var assembly = type.Assembly;
        if (!_testAssemblies.TryGetValue(assembly, out bool isTest))
        {
            // References to NUnit/TestRunner can also occur in a mixed Assembly-CSharp.
            // Only exclude assemblies explicitly designated for tests in their asmdef.
            string path = UnityEditor.Compilation.CompilationPipeline
                .GetAssemblyDefinitionFilePathFromAssemblyName(assembly.GetName().Name);
            isTest = false;
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(path);
                if (asset != null)
                {
                    var definition = JsonUtility.FromJson<AssemblyDefinition>(asset.text);
                    isTest = definition != null &&
                        ((definition.optionalUnityReferences?.Contains("TestAssemblies") ?? false) ||
                         (definition.defineConstraints?.Contains("UNITY_INCLUDE_TESTS") ?? false));
                }
            }
            _testAssemblies[assembly] = isTest;
        }
        return isTest;
    }

    /// <summary>States in registration order: by EntryPoint priority, the first one is the start state</summary>
    public static List<Type> CollectEntryPoints()
    {
        return TypeCache.GetTypesWithAttribute<EntryPointAttribute>()
            .Where(t => !t.IsAbstract && typeof(ABaseEntryPoint).IsAssignableFrom(t) && !IsTestType(t))
            .Select(t => (type: t, attribute: t.GetCustomAttribute<EntryPointAttribute>()))
            .Where(x => !x.attribute.Ignore)
            .OrderBy(x => x.attribute.Priority)
            .ThenBy(x => x.type.Name)
            .Select(x => x.type)
            .ToList();
    }

    public static List<Type> CollectScenes()
    {
        return TypeCache.GetTypesDerivedFrom<ABaseScene>().Where(t => !t.IsAbstract && !IsTestType(t)).OrderBy(t => t.Name).ToList();
    }

    public static EntryPointsConfig FindEntryPointsConfig() => FindAsset<EntryPointsConfig>();
    public static ScenesMappingConfig FindScenesConfig() => FindAsset<ScenesMappingConfig>();

    private static bool SyncEntryPoints()
    {
        var config = FindEntryPointsConfig();
        bool created = config == null;
        if (created)
        {
            config = CreateAsset<EntryPointsConfig>(ENTRY_POINTS_ASSET);
            AssignIfEmpty<ApplicationScopesInstaller>("_scriptableStates", config);
        }

        if (!config.EditorSetEntryPoints(CollectEntryPoints().Select(TypeKey).ToList()))
            return created;
        EditorUtility.SetDirty(config);
        return true;
    }

    private static bool SyncScenes()
    {
        var config = FindScenesConfig();
        bool changed = config == null;
        if (changed)
        {
            config = CreateAsset<ScenesMappingConfig>(SCENES_ASSET);
            AssignIfEmpty<ScenesManagerSO>("_scenes", config);
        }

        // Entries of removed types are kept: the window reports them, so a renamed scene keeps its reference
        foreach (var type in CollectScenes())
        {
            string key = TypeKey(type);
            if (config.Scenes.Any(e => e.TypeName == key)) continue;
            config.Scenes.Add(new ScenesMappingConfig.SceneEntry { TypeName = key, Scene = new AssetReference() });
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(config);
        return changed;
    }

    private static T FindAsset<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(asset => asset != null);
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        if (!AssetDatabase.IsValidFolder(ROOT_PATH))
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(ROOT_PATH));
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
        return asset;
    }

    private static void AssignIfEmpty<TInstaller>(string field, UnityEngine.Object value) where TInstaller : ScriptableObject
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(TInstaller).Name}"))
        {
            var installer = AssetDatabase.LoadAssetAtPath<TInstaller>(AssetDatabase.GUIDToAssetPath(guid));
            if (installer == null) continue;
            var serialized = new SerializedObject(installer);
            var property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) continue;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

#endif
