#if HAS_VOODYA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Voodya.Core;
using Object = UnityEngine.Object;

/// <summary>
/// Editor model of the architecture config: services, scopes and problems of the configuration.
/// Global scope is BootstrapEntryPoint, every other ABaseEntryPoint gets its own child scope.
/// Source of truth is the CompositeInstaller of each scope; ArchConfigData keeps references to them.
/// Used by ArchConfigWindow and by build validation.
/// </summary>
public class ArchConfigModel
{
    public const string ROOT_PATH = VooConfigSync.ROOT_PATH;
    public const string COMPOSITE_FOLDER = "COMPOSIT_INSTALLERS";
    public const string SERVICES_FOLDER = "SERVICES_INSTALLERS";
    public const string CONFIG_NAME = "ArchConfigData.asset";
    public const string BOOTSTRAP_SCENE = "Bootstrap";

    public static readonly string GlobalScopeName = nameof(BootstrapEntryPoint);

    public class ServiceInfo
    {
        public Type ServiceType;
        public Type InstallerType;
        public ScriptableInstaller Installer;
        public string Error;
        public string Name => ServiceType.Name;
    }

    public class ScopeInfo
    {
        public string Name;
        public Type EntryType;
        public CompositeInstaller Composite;
        public bool IsGlobal;
        public bool IsStart;
        public bool IsRegistered;
        public bool IsIgnored;
        public int Priority;
        public bool IsStale => EntryType == null;
        public string Title => IsGlobal ? "Global" : Name;
        public List<ScriptableInstaller> Installers => Composite != null ? Composite._installers : new List<ScriptableInstaller>();
    }

    public class Issue
    {
        public MessageType Type;
        public string Text;
        public string FixLabel;
        public Action Fix;
    }

    public ArchConfigData Config { get; private set; }
    public List<ServiceInfo> Services { get; private set; } = new();
    public List<ScopeInfo> Scopes { get; private set; } = new();
    public List<Issue> Issues { get; private set; } = new();
    public HashSet<Object> ProblemInstallers { get; } = new();
    public ScopeInfo GlobalScope => Scopes.FirstOrDefault(s => s.IsGlobal);

    /// <summary>Raised after data or problems have changed</summary>
    public event Action Changed;

    private Dictionary<Type, ServiceInfo> _serviceByInstallerType = new();
    private bool _createMissing = true;

    #region Loading

    /// <param name="createMissing">Create missing installer and composite assets (false for read-only validation)</param>
    public void Reload(bool createMissing = true)
    {
        _createMissing = createMissing;
        if (createMissing)
        {
            EnsureFolder(ROOT_PATH);
            EnsureFolder($"{ROOT_PATH}/{COMPOSITE_FOLDER}");
            EnsureFolder($"{ROOT_PATH}/{SERVICES_FOLDER}");
        }

        Config = LoadConfig();
        Services = new List<ServiceInfo>();
        Scopes = new List<ScopeInfo>();
        _serviceByInstallerType = new Dictionary<Type, ServiceInfo>();
        if (Config == null)
        {
            BuildIssues();
            return;
        }

        Config._serviceInstallers ??= new CustomDictionary<string, ScriptableInstaller>(new Dictionary<string, ScriptableInstaller>());
        Config._compositeInstallers ??= new CustomDictionary<string, CompositeInstaller>(new Dictionary<string, CompositeInstaller>());
        bool changed = false;

        foreach (var type in TypeCache.GetTypesWithAttribute<ServiceAttribute>().Where(IsProjectClass).OrderBy(t => t.Name))
        {
            var info = new ServiceInfo { ServiceType = type };
            try
            {
                info.InstallerType = type.GetCustomAttribute<ServiceAttribute>().InstallerType;
            }
            catch (Exception e)
            {
                info.Error = (e.InnerException ?? e).Message;
            }
            if (info.InstallerType != null)
            {
                info.Installer = FindOrCreateInstaller(info.InstallerType, ref changed);
                _serviceByInstallerType[info.InstallerType] = info;
            }
            Services.Add(info);
        }

        string startName = StartStateName();
        foreach (var type in TypeCache.GetTypesDerivedFrom<ABaseEntryPoint>().Where(IsProjectClass))
        {
            var attribute = type.GetCustomAttribute<EntryPointAttribute>();
            var scope = new ScopeInfo
            {
                Name = type.Name,
                EntryType = type,
                IsGlobal = type.Name == GlobalScopeName,
                IsRegistered = attribute != null,
                IsIgnored = attribute?.Ignore ?? false,
                Priority = attribute?.Priority ?? 100,
                IsStart = type.Name == startName,
            };
            scope.Composite = FindOrCreateComposite(scope.Name, ref changed);
            Scopes.Add(scope);
        }

        // Scopes left in config after their entry point class was removed or renamed
        foreach (var key in Config._compositeInstallers.Keys.ToList())
        {
            if (Scopes.Any(s => s.Name == key)) continue;
            Scopes.Add(new ScopeInfo { Name = key, Composite = Config._compositeInstallers[key], Priority = int.MaxValue });
        }

        foreach (var scope in Scopes)
        {
            if (scope.Composite != null && scope.Composite._installers == null)
            {
                scope.Composite._installers = new List<ScriptableInstaller>();
                EditorUtility.SetDirty(scope.Composite);
            }
        }

        Scopes = Scopes
            .OrderByDescending(s => s.IsGlobal)
            .ThenBy(s => s.IsStale)
            .ThenByDescending(s => s.IsStart)
            .ThenBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .ToList();

        if (changed && createMissing)
            SaveConfig();
        BuildIssues();
    }

    private ArchConfigData LoadConfig()
    {
        string path = $"{ROOT_PATH}/{CONFIG_NAME}";
        var config = AssetDatabase.LoadAssetAtPath<ArchConfigData>(path);
        if (config != null || !_createMissing)
            return config;

        config = ScriptableObject.CreateInstance<ArchConfigData>();
        AssetDatabase.CreateAsset(config, path);
        return config;
    }

    private ScriptableInstaller FindOrCreateInstaller(Type installerType, ref bool changed)
    {
        string key = installerType.Name;
        var installer = Config._serviceInstallers[key];
        if (installer != null && installer.GetType() == installerType)
            return installer;

        installer = FindAsset(installerType) as ScriptableInstaller;
        if (installer == null && _createMissing)
            installer = (ScriptableInstaller)CreateAsset(installerType, $"{ROOT_PATH}/{SERVICES_FOLDER}/{key}.asset");
        if (installer == null)
            return null;

        Config._serviceInstallers[key] = installer;
        changed = true;
        return installer;
    }

    private CompositeInstaller FindOrCreateComposite(string scopeName, ref bool changed)
    {
        var composite = Config._compositeInstallers[scopeName];
        if (composite != null)
            return composite;

        string basePath = $"{ROOT_PATH}/{COMPOSITE_FOLDER}/{scopeName}Composite";
        composite = AssetDatabase.LoadAssetAtPath<CompositeInstaller>(basePath + ".asset");
        if (composite == null)
            composite = AssetDatabase.LoadAssetAtPath<CompositeInstaller>(basePath + ".Asset");
        if (composite == null && _createMissing)
            composite = (CompositeInstaller)CreateAsset(typeof(CompositeInstaller), basePath + ".asset");
        if (composite == null)
            return null;

        Config._compositeInstallers[scopeName] = composite;
        changed = true;
        return composite;
    }

    private static string StartStateName()
    {
        var config = VooConfigSync.FindEntryPointsConfig();
        string first = config != null ? config.EntryPoints.FirstOrDefault() : null;
        return first != null ? Type.GetType(first)?.Name : null;
    }

    #endregion

    #region Editing

    public void SaveConfig()
    {
        if (Config == null) return;

        // Service names per scope, kept for backward compatibility with older tools
        var names = new Dictionary<string, List<string>>();
        foreach (var scope in Scopes)
            names[scope.Name] = scope.Installers.Where(i => i != null).Select(DisplayName).ToList();
        Config._stringsData = new CustomDictionary<string, List<string>>(names);

        EditorUtility.SetDirty(Config);
        AssetDatabase.SaveAssetIfDirty(Config);
    }

    public bool CanAdd(ScopeInfo scope, ScriptableInstaller installer)
    {
        return scope?.Composite != null
            && installer != null
            && !(installer is ArchConfigData)
            && Scopes.All(s => s.Composite != installer)
            && !scope.Composite._installers.Contains(installer);
    }

    public void AddInstaller(ScopeInfo scope, ScriptableInstaller installer)
    {
        if (!CanAdd(scope, installer)) return;
        Undo.RecordObject(scope.Composite, $"Add {DisplayName(installer)} to {scope.Title}");
        scope.Composite._installers.Add(installer);
        Commit(scope.Composite);
    }

    public void RemoveInstaller(ScopeInfo scope, ScriptableInstaller installer)
    {
        if (scope.Composite == null || !scope.Composite._installers.Contains(installer)) return;
        Undo.RecordObject(scope.Composite, $"Remove {DisplayName(installer)} from {scope.Title}");
        scope.Composite._installers.Remove(installer);
        Commit(scope.Composite);
    }

    public void RemoveEmptySlots(ScopeInfo scope)
    {
        Undo.RecordObject(scope.Composite, $"Clean {scope.Title}");
        scope.Composite._installers.RemoveAll(i => i == null);
        Commit(scope.Composite);
    }

    public void MoveInstaller(ScopeInfo from, ScopeInfo to, ScriptableInstaller installer)
    {
        if (from == null || from == to)
        {
            AddInstaller(to, installer);
            return;
        }
        if (!CanAdd(to, installer)) return;
        Undo.RecordObjects(new Object[] { from.Composite, to.Composite }, $"Move {DisplayName(installer)} to {to.Title}");
        from.Composite._installers.Remove(installer);
        to.Composite._installers.Add(installer);
        Commit(from.Composite, to.Composite);
    }

    /// <summary>Saves composites changed outside of the model, e.g. by Undo</summary>
    public void SaveDirtyComposites()
    {
        foreach (var scope in Scopes)
            if (scope.Composite != null)
                AssetDatabase.SaveAssetIfDirty(scope.Composite);
        SaveConfig();
        BuildIssues();
    }

    private void Commit(params CompositeInstaller[] composites)
    {
        foreach (var composite in composites)
        {
            EditorUtility.SetDirty(composite);
            AssetDatabase.SaveAssetIfDirty(composite);
        }
        SaveConfig();
        BuildIssues();
    }

    public void RemoveStaleScope(ScopeInfo scope)
    {
        if (!EditorUtility.DisplayDialog("Remove scope",
            $"Remove scope '{scope.Name}' from ArchConfigData?\nIts composite installer asset is kept in the project.", "Remove", "Cancel"))
            return;

        Config._compositeInstallers.Remove(scope.Name);
        Scopes.Remove(scope);
        SaveConfig();
        Reload();
    }

    public void SyncConfigs()
    {
        VooConfigSync.Sync();
        Reload();
    }

    private void RemoveSceneEntry(ScenesMappingConfig config, ScenesMappingConfig.SceneEntry entry)
    {
        Undo.RecordObject(config, $"Remove {entry.TypeName} scene");
        config.Scenes.Remove(entry);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        BuildIssues();
    }

    private void PutBootstrapSceneFirst(string path)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        BuildIssues();
    }

    private void RemoveMissingBuildScenes()
    {
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => File.Exists(s.path)).ToArray();
        BuildIssues();
    }

    #endregion

    #region Issues

    public void BuildIssues()
    {
        Issues = new List<Issue>();
        ProblemInstallers.Clear();

        if (Config == null)
        {
            AddIssue(MessageType.Error, $"{ROOT_PATH}/{CONFIG_NAME} not found. Open VooArch/Voo arch configure to create it.");
        }
        else
        {
            CheckServices();
            CheckScopes();
            CheckInstallerFields();
        }
        CheckConfigs();
        CheckBuildSettings();

        Issues = Issues.OrderBy(i => i.Type == MessageType.Error ? 0 : i.Type == MessageType.Warning ? 1 : 2).ToList();
        Changed?.Invoke();
    }

    private void CheckServices()
    {
        foreach (var service in Services)
        {
            if (service.Error != null)
                AddIssue(MessageType.Error, $"{service.Name}: {service.Error}", "Open script", () => OpenScript(service.ServiceType));
            else if (service.InstallerType == null)
                AddIssue(MessageType.Warning, $"{service.Name} has no installer, so it can't be added to a scope. Use [Service(typeof({service.Name}Installer))].",
                    "Open script", () => OpenScript(service.ServiceType));
            else if (service.Installer == null)
                AddIssue(MessageType.Error, $"Installer asset of {service.Name} ({service.InstallerType.Name}) not found.", "Create", () => Reload());
            else if (ScopesOf(service.Installer).Count == 0)
                AddIssue(MessageType.Info, $"{service.Name} is not used in any scope.");
        }
    }

    private void CheckScopes()
    {
        var global = GlobalScope;
        if (global == null)
            AddIssue(MessageType.Error, $"{GlobalScopeName} not found, global scope can't be configured.");

        foreach (var scope in Scopes)
        {
            if (scope.IsStale)
            {
                AddIssue(MessageType.Warning, $"Scope '{scope.Name}' is in config, but its entry point class no longer exists.", "Remove", () => RemoveStaleScope(scope));
                continue;
            }
            if (!scope.IsGlobal && !scope.IsRegistered)
                AddIssue(MessageType.Warning, $"{scope.Name} has no [EntryPoint] attribute, so it is not registered as an application state.",
                    "Open script", () => OpenScript(scope.EntryType));
            if (scope.Composite == null)
            {
                AddIssue(MessageType.Error, $"{scope.Title} has no composite installer.", "Create", () => Reload());
                continue;
            }
            if (scope.Installers.Any(i => i == null))
                AddIssue(MessageType.Error, $"{scope.Title} has empty installer slots (asset was deleted).", "Clean", () => RemoveEmptySlots(scope));
            if (!scope.IsGlobal && global != null)
            {
                foreach (var installer in scope.Installers.Where(i => i != null && global.Installers.Contains(i)))
                    AddIssue(MessageType.Warning, $"{DisplayName(installer)} is registered in Global and in {scope.Name}: the scope gets its own instance instead of the global one.");
            }
        }
    }

    private void CheckInstallerFields()
    {
        foreach (var installer in Scopes.SelectMany(s => s.Installers).Where(i => i != null && !(i is CompositeInstaller)).Distinct())
        {
            foreach (var property in TopLevelProperties(installer))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                {
                    ProblemInstallers.Add(installer);
                    AddIssue(MessageType.Error, $"{DisplayName(installer)}: field '{property.displayName}' is not assigned.", "Select", () => SelectAsset(installer));
                }
            }
        }
    }

    private void CheckConfigs()
    {
        var entryPoints = VooConfigSync.FindEntryPointsConfig();
        if (entryPoints == null)
        {
            AddIssue(MessageType.Error, "No EntryPointsConfig asset, states are not registered.", "Sync", SyncConfigs);
        }
        else
        {
            var expected = VooConfigSync.CollectEntryPoints().Select(VooConfigSync.TypeKey).ToList();
            var missing = entryPoints.EntryPoints.Where(n => Type.GetType(n) == null).ToList();
            if (missing.Count > 0)
                AddIssue(MessageType.Error, $"Entry points config references missing classes: {string.Join(", ", missing)}.", "Sync", SyncConfigs);
            else if (!entryPoints.EntryPoints.SequenceEqual(expected))
                AddIssue(MessageType.Warning, "Entry points config is out of date.", "Sync", SyncConfigs);
            if (expected.Count == 0)
                AddIssue(MessageType.Error, "No classes with [EntryPoint] attribute, the app has no start state.");
        }

        var scenes = VooConfigSync.FindScenesConfig();
        if (scenes == null)
        {
            AddIssue(MessageType.Error, "No ScenesMappingConfig asset, scenes are not registered.", "Sync", SyncConfigs);
            return;
        }

        var notMapped = VooConfigSync.CollectScenes().Select(VooConfigSync.TypeKey).Where(k => scenes.Scenes.All(e => e.TypeName != k)).ToList();
        if (notMapped.Count > 0)
            AddIssue(MessageType.Warning, $"Scenes mapping is out of date, missing: {string.Join(", ", notMapped)}.", "Sync", SyncConfigs);

        foreach (var entry in scenes.Scenes.ToList())
        {
            var type = Type.GetType(entry.TypeName);
            if (type == null)
                AddIssue(MessageType.Warning, $"Scenes mapping has an entry for missing class '{entry.TypeName}'.", "Remove", () => RemoveSceneEntry(scenes, entry));
            else if (entry.Scene == null || string.IsNullOrEmpty(entry.Scene.AssetGUID))
                AddIssue(MessageType.Error, $"Scene for {type.Name} is not set in {scenes.name}.", "Select", () => SelectAsset(scenes));
        }
    }

    private void CheckBuildSettings()
    {
        string bootstrapPath = AssetDatabase.FindAssets($"{BOOTSTRAP_SCENE} t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == BOOTSTRAP_SCENE);
        if (bootstrapPath == null)
        {
            AddIssue(MessageType.Error, $"Scene '{BOOTSTRAP_SCENE}' not found: BootstrapInstaller starts the app only in a scene with this name.");
        }
        else
        {
            var firstScene = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            if (firstScene == null || firstScene.path != bootstrapPath)
                AddIssue(MessageType.Error, $"'{BOOTSTRAP_SCENE}' must be the first enabled scene in Build Settings.", "Fix", () => PutBootstrapSceneFirst(bootstrapPath));
        }

        var missing = EditorBuildSettings.scenes.Where(s => !File.Exists(s.path)).Select(s => s.path).ToList();
        if (missing.Count > 0)
            AddIssue(MessageType.Warning, $"Build Settings reference missing scenes: {string.Join(", ", missing)}.", "Remove", RemoveMissingBuildScenes);
    }

    private void AddIssue(MessageType type, string text, string fixLabel = null, Action fix = null)
    {
        Issues.Add(new Issue { Type = type, Text = text, FixLabel = fixLabel, Fix = fix });
    }

    #endregion

    #region Helpers

    private static bool IsProjectClass(Type type) => type.IsClass && !type.IsAbstract && !VooConfigSync.IsTestType(type);

    public List<ScopeInfo> ScopesOf(ScriptableInstaller installer)
    {
        return Scopes.Where(s => s.Installers.Contains(installer)).ToList();
    }

    public ServiceInfo ServiceOf(ScriptableInstaller installer)
    {
        return installer != null && _serviceByInstallerType.TryGetValue(installer.GetType(), out var service) ? service : null;
    }

    public string DisplayName(ScriptableInstaller installer)
    {
        return ServiceOf(installer)?.Name ?? installer.name;
    }

    public static IEnumerable<SerializedProperty> TopLevelProperties(Object target)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        if (!property.NextVisible(true)) yield break;
        do
        {
            if (property.propertyPath != "m_Script")
                yield return property.Copy();
        }
        while (property.NextVisible(false));
    }

    private static Object FindAsset(Type type)
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (asset != null && asset.GetType() == type)
                    return asset;
        }
        return null;
    }

    private static Object CreateAsset(Type type, string path)
    {
        var asset = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    public static void SelectAsset(Object asset)
    {
        if (asset == null) return;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    public static void OpenScript(Type type)
    {
        if (type == null) return;
        foreach (var guid in AssetDatabase.FindAssets($"{type.Name} t:MonoScript"))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (script != null && (script.GetClass() == type || script.name == type.Name))
            {
                AssetDatabase.OpenAsset(script);
                return;
            }
        }
        Debug.LogWarning($"[VooArch] Script for {type.Name} not found");
    }

    #endregion
}
#endif
