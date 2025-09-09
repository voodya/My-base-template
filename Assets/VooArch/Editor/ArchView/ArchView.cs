using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if HAS_VOODYA
using Voodya.Core;
using static UnityEngine.EventSystems.EventTrigger;

#endif
using Module = UnityEditor.EditorGUILayout;
#if !HAS_VOODYA
[InitializeOnLoad]
public static class VooResolver
{
    static VooResolver()
    {
        // This code runs when the editor loads.
        // You can add logic here to check if your specific Git package is installed
        // and then call your desired method.
        Debug.Log("Editor loaded. Checking for Git package...");
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
                //Debug.Log("manifest.json content:\n" + manifestContent);
                if (!dep.dependencies.ContainsKey("jp.hadashikick.vcontainer"))
                    dep.dependencies["jp.hadashikick.vcontainer"] = "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer";
                else
                    Debug.LogError("VContainer exist");
                if (!dep.dependencies.ContainsKey("com.cysharp.unitask"))
                    dep.dependencies["com.cysharp.unitask"] = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
                else
                    Debug.LogError("UniTask exist");

                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(dep));
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error reading manifest.json: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("manifest.json not found at: " + manifestPath);
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
        Type type = typeof(NamedBuildTarget);
        var props = type.GetProperties().ToList();
        List<BuildTargetGroup> groups = Enum.GetValues(typeof(BuildTargetGroup)).Cast<BuildTargetGroup>().ToList();

        foreach (var prop in groups)
        {
            //if (prop == BuildTargetGroup.Unknown) continue;
            try
            {
                NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(prop);
                string symb = PlayerSettings.GetScriptingDefineSymbols(target);
                if (!symb.Split(";").ToList().Any(x => x.Equals(define)))
                {
                    symb += $";{define}";
                    PlayerSettings.SetScriptingDefineSymbols(target, symb);
                }
            }
            catch (Exception e)
            {
                //Debug.LogError($"No have group {e}");
            }

        }
    }

}
#if HAS_VOODYA




[InitializeOnLoad]
public static class VooSourceGen
{
    private static List<Type> _entrys;
    private static List<Type> _scenes;
    static VooSourceGen()
    {
        Generate();
    }

    private static void Generate()
    {
        _entrys = FindClassesWithAttribute<EntryPointAttribute>();
        _scenes = FindDerivedClasses<ABaseScene>();
        string path = Path.Combine(Application.dataPath, "..", "Assets", "ARCH_CONFIGURE", "AUTOGEN_EP.cs");
        string pathScenes = Path.Combine(Application.dataPath, "..", "Assets", "ARCH_CONFIGURE", "AUTOGEN_SCENES.cs");
        EntryPointSOGenerator.Generate(_entrys, path);
        AddressableSceneGenerator.Generate(_scenes, pathScenes);
        VooHelpers.AddNewScriptingDefineSymbols("GENERATED_VOODYA");
    }


    public static List<Type> FindDerivedClasses<TBaseType>()
    {
        Type baseType = typeof(TBaseType);
        List<Type> derivedTypes = new List<Type>();

        // Получаем все сборки, загруженные в текущем домене приложения.
        // В Unity это обычно включает сборки вашего проекта, Unity-сборки и т.д.
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                // Пытаемся получить все типы из сборки.
                // Некоторые сборки могут вызвать исключение, если к ним нет доступа.
                Type[] typesInAssembly = assembly.GetTypes();

                foreach (Type type in typesInAssembly)
                {
                    // Проверяем, что тип является классом, не является абстрактным и наследуется от базового типа.
                    // baseType.IsAssignableFrom(type) - это более надежный способ проверки наследования,
                    // так как он работает как для классов, так и для интерфейсов.
                    // type.IsSubclassOf(baseType) - работает только для классов.
                    if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
                    {
                        derivedTypes.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Обработка исключений, если некоторые типы не могут быть загружены.
                // Это может произойти в Unity, если есть проблемы с DLL или скриптами.
                Debug.LogWarning($"ReflectionTypeLoadException при загрузке сборки {assembly.FullName}: {ex.Message}");
                foreach (Exception loaderEx in ex.LoaderExceptions)
                {
                    Debug.LogWarning($"Loader Exception: {loaderEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при получении типов из сборки {assembly.FullName}: {ex.Message}");
            }
        }

        return derivedTypes;
    }

    public static List<Type> FindClassesWithAttribute<TAttribute>() where TAttribute : Attribute
    {
        return AppDomain.CurrentDomain.GetAssemblies() // 1. Получаем все сборки
            .SelectMany(assembly => assembly.GetTypes()) // 2. Получаем все типы из всех сборок
            .Where(type =>
                type.IsClass &&                           // 3. Убеждаемся, что это класс
                !type.IsAbstract &&                       // 4. Исключаем абстрактные классы
                type.IsDefined(typeof(TAttribute)))       // 5. Проверяем наличие атрибута
            .ToList(); // 6. Возвращаем результат в виде списка



    }
}


/// <summary>
/// Генерирует класс-наследник ScriptableStates для регистрации
/// точек входа (Entry Points) приложения.
/// </summary>
public static class EntryPointSOGenerator
{
    /// <summary>
    /// Создает C#-файл на основе списка типов.
    /// </summary>
    /// <param name="entryTypes">Список типов точек входа.</param>
    /// <param name="outputPath">Полный путь для сохранения файла (например, "Assets/Scripts/Generated/AUTOGEN_EP.cs").</param>
    public static void Generate(List<Type> entryTypes, string outputPath)
    {
        // 1. Проверка входных данных
        if (entryTypes == null || !entryTypes.Any())
        {
            throw new ArgumentException("Список типов для генерации не может быть пустым.", nameof(entryTypes));
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Путь для сохранения файла не может быть пустым.", nameof(outputPath));
        }

        var sb = new StringBuilder();

        // 2. Заголовок и using'и
        sb.AppendLine("// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.");
        sb.AppendLine("using System;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using VContainer;");
        sb.AppendLine("using Voodya.Core;");
        sb.AppendLine();

        // 3. Определение класса с атрибутом CreateAssetMenu
        sb.AppendLine("[CreateAssetMenu(fileName = \"EntrysMapping\", menuName = \"Voodya/Entry Points Mapping\")]");
        sb.AppendLine("public class AUTOGEN_EP : ScriptableStates");
        sb.AppendLine("{");

        // 4. Генерация метода OnRegister
        sb.AppendLine("    public override void OnRegister(IApplicationScopesService applicationScopesService, IObjectResolver objectResolver)");
        sb.AppendLine("    {");

        // Устанавливаем тип первого состояния
        entryTypes.Sort((type1, type2) =>
        {
            var priority1 = type1.GetCustomAttribute<EntryPointAttribute>()?.Priority ?? int.MaxValue;
            var priority2 = type2.GetCustomAttribute<EntryPointAttribute>()?.Priority ?? int.MaxValue;
            return priority1.CompareTo(priority2);
        });
        string firstTypeName = entryTypes.First().Name;
        sb.AppendLine($"        _firstStateType = typeof({firstTypeName});");
        sb.AppendLine(); // Пустая строка для читаемости

        // Регистрируем все состояния из списка
        foreach (var type in entryTypes)
        {
            string typeName = type.Name;
            sb.AppendLine($"        applicationScopesService.Register(new DefaultState<{typeName}>(objectResolver));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        // 5. Сохранение файла
        try
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log($"[EntryPointSOGenerator] Файл успешно сгенерирован: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EntryPointSOGenerator] Ошибка при записи файла: {ex.Message}");
        }
    }
}

/// <summary>
/// Генерирует класс-наследник BaseScenesSO для регистрации
/// сцен из Addressables.
/// </summary>
public static class AddressableSceneGenerator
{
    /// <summary>
    /// Создает C#-файл на основе списка типов.
    /// </summary>
    /// <param name="sceneTypes">Список типов (классов-контроллеров) сцен.</param>
    /// <param name="outputPath">Полный путь для сохранения файла (например, "Assets/Scripts/Generated/AUTOGEN_SCENES_MAPPING.cs").</param>
    public static void Generate(List<Type> sceneTypes, string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Путь для сохранения файла не может быть пустым.", nameof(outputPath));
        }

        sceneTypes ??= new List<Type>();

        var sb = new StringBuilder();

        // 1. Заголовок и using'и
        sb.AppendLine("// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.");
        sb.AppendLine("using Voodya.Core;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.AddressableAssets;");
        sb.AppendLine();

        // 2. Определение класса
        // Атрибут для создания ассета из меню Unity
        sb.AppendLine("[CreateAssetMenu(fileName = \"ScenesMapping\", menuName = \"Voodya/Scenes Mapping\")]");
        sb.AppendLine("public class AUTOGEN_SCENES_MAPPING : BaseScenesSO");
        sb.AppendLine("{");

        // 3. Генерация полей [SerializeField]
        if (sceneTypes.Any())
        {
            sb.AppendLine("    // Поля для привязки ассетов сцен в инспекторе");
            foreach (var type in sceneTypes)
            {
                string typeName = type.Name;
                // Превращаем "LoadingScene" в "_loadingScene"
                string fieldName = $"_{char.ToLower(typeName[0])}{typeName.Substring(1)}";

                sb.AppendLine($"    [SerializeField] private AssetReference {fieldName};");
            }
            sb.AppendLine();
        }

        // 4. Генерация метода OnRegister
        sb.AppendLine("    public override void OnRegister(ScenesManagerService service)");
        sb.AppendLine("    {");
        foreach (var type in sceneTypes)
        {
            string typeName = type.Name;
            string fieldName = $"_{char.ToLower(typeName[0])}{typeName.Substring(1)}";

            sb.AppendLine($"        service.RegisterScene(typeof({typeName}), {fieldName});");
        }
        sb.AppendLine("    }");

        // 5. Завершение
        sb.AppendLine("}");

        // Сохранение файла
        try
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log($"[AddressableSceneGenerator] Файл успешно сгенерирован: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AddressableSceneGenerator] Ошибка при записи файла: {ex.Message}");
        }
    }
}

public class ArchView : EditorWindow
{

    private Dictionary<string, List<string>> _currentData;
    private Dictionary<string, ServiceData> _servicesData;

     
    private bool isDragging = false;
    private string draggedItem;
    private string beforeDrag;
    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 scrollPositionPropertys = Vector2.zero;
    private string globalScopeName = typeof(BootstrapEntryPoint).Name;
    private List<Type> _entrys;
    private List<string> _targetServices;
    private Dictionary<string, Vector2> _customScrolls = new Dictionary<string, Vector2>();

    #region Const strings

    private const string RAW_ASSET_NAME = "ArchConfigData.asset";
    private const string ALL_ASSETS_PATH = "Assets/ARCH_CONFIGURE";
    private const string ASSETS_FOLDER_NAME = "ARCH_CONFIGURE";
    private const string COMPOSITE_FOLDER_NAME = "COMPOSIT_INSTALLERS";
    private const string SERVICES_FOLDER_NAME = "SERVICES_INSTALLERS";
    private const string INSTALLERS_TITLE = "All installers";

    #endregion

    private ArchConfigData _config;
    private CompositeInstaller _currentCompositeInstaller;

    [MenuItem("VooArch/Voo arch configure")]
    public static void ShowWindow()
    {
        GetWindow<ArchView>("Arch View");
    }

    private void OnEnable()
    {
        if (!AssetDatabase.AssetPathExists(ALL_ASSETS_PATH))
        {
            AssetDatabase.CreateFolder("Assets", ASSETS_FOLDER_NAME);
            AssetDatabase.CreateFolder($"Assets/{ASSETS_FOLDER_NAME}", COMPOSITE_FOLDER_NAME);
            AssetDatabase.CreateFolder($"Assets/{ASSETS_FOLDER_NAME}", SERVICES_FOLDER_NAME);
        }

        LoadData();
    }

    

    

    private void OnGUI()
    {

        scrollPosition = Module.BeginScrollView(scrollPosition);
        {
            Module.BeginHorizontal();
            {
                if (_currentData != null)
                {

                    DrawColumn(globalScopeName, _currentData[globalScopeName]);

                    foreach (var item in _currentData)
                    {
                        if (item.Key == globalScopeName) continue;
                        DrawColumn(item.Key, item.Value);
                    }
                }
                else
                {
                    Module.HelpBox("No have data", MessageType.Warning);
                }
            }
            Module.EndHorizontal();
        }
        Module.EndScrollView();



        Module.BeginVertical(GUI.skin.box);
        {
            scrollPositionPropertys = Module.BeginScrollView(scrollPositionPropertys);
            {
                if (_currentCompositeInstaller != null && _currentCompositeInstaller._installers != null && _currentCompositeInstaller._installers.Count != 0)
                {
                    Module.LabelField(_currentCompositeInstaller.name, EditorStyles.boldLabel);
                    SerializedObject serializedObject = new SerializedObject(_currentCompositeInstaller);
                    SerializedProperty prop = serializedObject.FindProperty("_installers");
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        DrowProperty(prop.GetArrayElementAtIndex(i).objectReferenceValue);
                    }
                }
                else
                {
                    Module.HelpBox("No have data", MessageType.Warning);
                }
            }
            Module.EndScrollView();
        }
        Module.EndVertical();


        HandleDragAndDrop();


        Module.BeginHorizontal();
        {
            if (GUILayout.Button("loadData")) LoadData();
            if (GUILayout.Button("Save")) SaveData();
        }
        Module.EndHorizontal();

        void DrowProperty(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                EditorGUI.indentLevel++;
                Editor editor = Editor.CreateEditor(obj);
                editor.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }
        }
    }

    

    private void LoadData()
    {
        _servicesData = new Dictionary<string, ServiceData>();
        _currentData = new Dictionary<string, List<string>>();
        _entrys = VooSourceGen.FindDerivedClasses<ABaseEntryPoint>();
        _config = AssetDatabase.LoadAssetAtPath(ALL_ASSETS_PATH + "/" + RAW_ASSET_NAME, typeof(ArchConfigData)) as ArchConfigData;
        if (_config == null)
        {
            _config = CreateNewConfig();
        }
        var services = VooSourceGen.FindClassesWithAttribute<ServiceAttribute>();
        _targetServices = services.Select(x => x.Name).ToList();
        _currentData = _config._stringsData.GetRawDictionary();

        List<string> mappedServices = new List<string>();

        foreach (var service in _currentData)
        {
            if (service.Key != INSTALLERS_TITLE)
                mappedServices.AddRange(service.Value);
        }

        foreach (var entry in _entrys)
        {
            if (!_currentData.ContainsKey(entry.Name))
                _currentData.Add(entry.Name, new List<string>());
        }

        List<ServiceData> newDatas = new List<ServiceData>();
        foreach (var item in services)
        {
            var attribute = (ServiceAttribute)Attribute.GetCustomAttribute(item, typeof(ServiceAttribute));
            string installerName = attribute.InstallerType != null ? attribute.InstallerType.Name : "nullstr";
            Debug.Log($"Сервис: {item.Name}, Установщик: {installerName}");
            ServiceData data = new ServiceData(item, attribute.InstallerType);
            newDatas.Add(data);
            _servicesData[item.Name] = data;
        }

        List<string> newServices = new List<string>();
        foreach (var service in _targetServices)
        {
            if (!mappedServices.Contains(service))
                newServices.Add(service);
        }


        _currentData[INSTALLERS_TITLE] = newServices;
        CreateSceneConfig();
        CreateEntrysConfig();
        CreateInstallers(_entrys, newDatas);
    }

    private void CreateEntrysConfig()
    {
        if (!AssetDatabase.AssetPathExists($"{ALL_ASSETS_PATH}/EntrysMapping.asset"))
        {
#if GENERATED_VOODYA
            var obj = new AUTOGEN_EP();
            AssetDatabase.CreateAsset(obj, ALL_ASSETS_PATH + "/EntrysMapping.asset");
#endif
        }
    }

    private void CreateSceneConfig()
    {
        if (!AssetDatabase.AssetPathExists($"{ALL_ASSETS_PATH}/ScenesMapping.asset"))
        {
#if GENERATED_VOODYA
            var obj = new AUTOGEN_SCENES_MAPPING();
            AssetDatabase.CreateAsset(obj, ALL_ASSETS_PATH + "/ScenesMapping.asset");
#endif
        }
    }

    private ArchConfigData CreateNewConfig()
    {
        var obj = new ArchConfigData();
        obj._stringsData = new CustomDictionary<string, List<string>>(new Dictionary<string, List<string>>());
        AssetDatabase.CreateAsset(obj, ALL_ASSETS_PATH + "/" + RAW_ASSET_NAME);
        return obj;
    }

    private void CreateInstallers(List<Type> entryPoints, List<ServiceData> services)
    {
        if (!Directory.Exists($"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/"))
        {
            Directory.CreateDirectory($"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/");
        }
        if (!Directory.Exists($"{ALL_ASSETS_PATH}/{SERVICES_FOLDER_NAME}/"))
        {
            Directory.CreateDirectory($"{ALL_ASSETS_PATH}/{SERVICES_FOLDER_NAME}/");
        }
        if (!AssetDatabase.AssetPathExists($"{ALL_ASSETS_PATH}/{RAW_ASSET_NAME}"))
        {
            var obj = new ArchConfigData();
            AssetDatabase.CreateAsset(obj, ALL_ASSETS_PATH + "/" + RAW_ASSET_NAME);
        }

        foreach (var entryPoint in entryPoints)
        {
            string fullPath = $"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/{entryPoint.Name}Composite.Asset";
            if (!AssetDatabase.AssetPathExists(fullPath))
            {
                AssetDatabase.CreateAsset(new CompositeInstaller(), fullPath);
                _config._compositeInstallers[entryPoint.Name] = AssetDatabase.LoadAssetAtPath<CompositeInstaller>(fullPath);
            }
            else
            {
                _config._compositeInstallers[entryPoint.Name] = AssetDatabase.LoadAssetAtPath<CompositeInstaller>(fullPath);
            }
        }

        foreach (var service in services)
        {
            if (service.InstallerType == null) continue;
            string fullPath = $"{ALL_ASSETS_PATH}/{SERVICES_FOLDER_NAME}/{service.InstallerName}.Asset";
            if (!AssetDatabase.AssetPathExists(fullPath))
            {
                AssetDatabase.CreateAsset(Activator.CreateInstance(service.InstallerType) as UnityEngine.Object, fullPath);
            }
            var obj = AssetDatabase.LoadAssetAtPath<ScriptableInstaller>(fullPath);
            _config._serviceInstallers[service.InstallerName] = obj;
        }
    }

    private void SaveData()
    {
        EditorUtility.SetDirty(_config);
    }

    private void DrawColumn(string title, List<string> itemList)
    {
        if (!_customScrolls.ContainsKey(title))
        {
            _customScrolls[title] = new Vector2();
        }


        GUIStyle columnStyle = new GUIStyle(GUI.skin.box);
        columnStyle.padding = new RectOffset(10, 10, 10, 10);
        columnStyle.margin = new RectOffset(5, 5, 5, 5);
        if (title == INSTALLERS_TITLE)
            columnStyle.normal.background = MakeTex(1, 1, "#266127");
        if (title == globalScopeName)
        {
            columnStyle.normal.background = MakeTex(1, 1, "#005550");
        }


        Module.BeginVertical(columnStyle, GUILayout.MinWidth(200), GUILayout.MinHeight(300), GUILayout.ExpandHeight(false));
        // Заголовок колонки
        if (title != globalScopeName)
        {
            Module.LabelField(title.Replace("EntryPoint", ""), EditorStyles.boldLabel);
        }
        else
        {
            Module.LabelField("Global application scope", EditorStyles.boldLabel);
        }
        _customScrolls[title] = Module.BeginScrollView(_customScrolls[title]);



        // Получаем прямоугольник для области колонки, чтобы отслеживать drop
        Rect columnRect = Module.GetControlRect(false, GUILayout.ExpandHeight(true));


        foreach (string item in itemList)
        {
            // Не отрисовываем элемент, который мы сейчас тащим
            if (isDragging && item == draggedItem)
            {
                // Оставляем пустое место для визуального фидбека
                GUIStyle placeholderStyle = new GUIStyle(GUI.skin.box);
                placeholderStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.3f));
                Module.LabelField("", placeholderStyle, GUILayout.Height(20));
            }
            else
            {
                DrawItem(item, title);
            }
        }

        if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
        {
            if (columnRect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    // Получаем перетаскиваемый объект
                    string itemToDrop = DragAndDrop.GetGenericData("DraggedItem") as string;
                    string installerName = _servicesData[itemToDrop].InstallerName;
                    if (itemToDrop != null)
                    {
                        //CompositeInstaller old = null;
                        //CompositeInstaller newComposite = AssetDatabase.LoadAssetAtPath($"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/{title}Composite.Asset", typeof(CompositeInstaller)) as CompositeInstaller;
                        //ScriptableInstaller installer = AssetDatabase.LoadAssetAtPath($"{ALL_ASSETS_PATH}/{SERVICES_FOLDER_NAME}/{installerName}.Asset", typeof(ScriptableInstaller)) as ScriptableInstaller;
                        //if (beforeDrag != INSTALLERS_TITLE)
                        //    old = AssetDatabase.LoadAssetAtPath($"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/{beforeDrag}Composite.Asset", typeof(CompositeInstaller)) as CompositeInstaller;
                        //if (old != null)
                        //{
                        //    old._installers?.Remove(installer);
                        //    EditorUtility.SetDirty(old);
                        //}
                        _currentData[beforeDrag].Remove(itemToDrop);
                        _currentData[title].Add(itemToDrop);
                        //if (newComposite != null)
                        //{
                        //    if (newComposite._installers == null) newComposite._installers = new List<ScriptableInstaller>();
                        //    newComposite._installers.Add(installer);
                        //    EditorUtility.SetDirty(newComposite);
                        //}


                        //itemList.Add(itemToDrop);
                        _config._stringsData = new CustomDictionary<string, List<string>>(_currentData);
                        // Сбрасываем состояние перетаскивания
                        draggedItem = null;
                        isDragging = false;
                        UpdateConfigState();
                    }
                    Event.current.Use(); // Сообщаем, что событие обработано
                }
            }
        }

        Module.EndScrollView();
        if (GUILayout.Button("Show scope fields"))
        {
            Debug.Log($"Try show {title}");
            _currentCompositeInstaller = AssetDatabase.LoadAssetAtPath($"{ALL_ASSETS_PATH}/{COMPOSITE_FOLDER_NAME}/{title}Composite.Asset", typeof(CompositeInstaller)) as CompositeInstaller;
        }
        Module.EndVertical();
    }

    private void UpdateConfigState()
    {
        foreach (var scope in _currentData)
        {
            if (scope.Key == INSTALLERS_TITLE) continue;
            CompositeInstaller composite = _config._compositeInstallers[scope.Key];
            composite._installers = new List<ScriptableInstaller>();
            foreach (string serviceName in scope.Value)
            {
                var serviceData = _servicesData[serviceName];
                if (serviceData.InstallerType != null)
                {

                    composite._installers.Add(_config._serviceInstallers[serviceData.InstallerName]);
                }
            }
            EditorUtility.SetDirty(composite);
        }
    }

    /// <summary>
    /// Отрисовывает один перетаскиваемый элемент.
    /// </summary>
    private void DrawItem(string item, string parent)
    {
        Module.BeginHorizontal();
        {
            GUIStyle itemStyle = new GUIStyle(EditorStyles.helpBox);
            if (_servicesData.ContainsKey(item))
            {
                ServiceData data = _servicesData[item];
                //data.ServiceType.Inter
                if (data.InstallerType != null)
                {
                    itemStyle.normal.textColor = Color.white;

                }
                else
                {
                    itemStyle.normal.background = MakeTex(1, 1, "#9C501D");
                    itemStyle.normal.textColor = Color.white;
                }
            }
            else
            {
                if (_servicesData.Select(x => x.Value).Any(y => y.InstallerType != null && y.InstallerType.Name == item))
                {
                    //itemStyle.normal.background = MakeTex(1, 1, "#9C501D");
                    itemStyle.normal.textColor = Color.white;
                }
                else
                {
                    itemStyle.normal.background = MakeTex(1, 1, "#9C501D");
                    Module.HelpBox("No service", MessageType.Warning);
                    itemStyle.normal.textColor = Color.white;
                }


            }

            Module.LabelField(item, itemStyle, GUILayout.Height(20));
            Rect itemRect = GUILayoutUtility.GetLastRect();

            if (GUILayout.Button("&", GUILayout.MaxWidth(20))) FindAndHighlightScript(item);

            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                isDragging = true;
                draggedItem = item;
                beforeDrag = parent;

                // Готовим данные для Drag and Drop
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData("DraggedItem", item);
                DragAndDrop.StartDrag(item);

                Event.current.Use(); // Сообщаем, что событие обработано
            }
        }
        Module.EndHorizontal();

    }

    /// <summary>
    /// Перегрузка метода для работы напрямую с типом Type.
    /// </summary>
    /// <param name="type">Тип класса для поиска.</param>
    public static void FindAndHighlightScript(Type type)
    {
        if (type != null)
        {
            FindAndHighlightScript(type.Name);
        }
    }

    /// <summary>
    /// Основной метод для поиска и подсветки файла.
    /// </summary>
    /// <param name="className">Имя класса для поиска.</param>
    public static void FindAndHighlightScript(string className)
    {
        // Ищем GUID ассета по имени скрипта. "t:script" фильтрует поиск только по скриптам.
        string[] guids = AssetDatabase.FindAssets($"{className} t:script");

        if (guids.Length == 0)
        {
            Debug.LogWarning($"Script with class '{className}' not found.");
            return;
        }

        // Обычно имя класса совпадает с именем файла, поэтому берем первый результат.
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);

        // Загружаем ассет по пути
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

        if (obj != null)
        {
            // ✨ Подсвечиваем объект в окне проекта
            EditorGUIUtility.PingObject(obj);

            // Если вы хотите не просто подсветить, а выбрать файл, используйте:
            // Selection.activeObject = obj;
        }
    }

    private void DrawItem(ServiceData item, string parent)
    {
        GUIStyle itemStyle = new GUIStyle(EditorStyles.helpBox);
        if (item.InstallerType != null)
        {
            itemStyle.normal.textColor = Color.white;

        }
        else
        {
            itemStyle.normal.background = MakeTex(1, 1, "#9C501D");
            itemStyle.normal.textColor = Color.white;
        }

        Module.LabelField(item.ServiceName, itemStyle, GUILayout.Height(20));
        Rect itemRect = GUILayoutUtility.GetLastRect();

        // Проверяем, началось ли перетаскивание этого элемента
        if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
        {
            isDragging = true;
            draggedItem = item.ServiceName;
            beforeDrag = parent;

            // Готовим данные для Drag and Drop
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("DraggedItem", item);
            DragAndDrop.StartDrag(item.ServiceName);

            Event.current.Use(); // Сообщаем, что событие обработано
        }
    }

    /// <summary>
    /// Обрабатывает отмену или завершение перетаскивания вне колонок.
    /// </summary>
    private void HandleDragAndDrop()
    {
        if (Event.current.type == EventType.DragExited || Event.current.type == EventType.MouseUp)
        {
            // Если перетаскивание завершилось, сбрасываем состояние
            if (isDragging)
            {
                isDragging = false;
                draggedItem = null;
                // Перерисовываем окно, чтобы убрать "призрачный" элемент
                Repaint();
            }
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private Texture2D MakeTex(int width, int height, string color)
    {
        Color col = Color.gray;
        ColorUtility.TryParseHtmlString(color, out col);
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}

#endif