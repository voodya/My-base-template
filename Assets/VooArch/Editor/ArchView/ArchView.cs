using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
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

    private static List<Type> _legacyEntrysTypes;
    private static List<Type> _legacyScenesTypes;

    static VooSourceGen()
    {
        Generate();
    }

    /// <param name="force">Rewrite files even if no new types were found (e.g. after removing an entry point)</param>
    public static void Generate(bool force = false)
    {
        _entrys = FindClassesWithAttribute<EntryPointAttribute>();
        _scenes = FindDerivedClasses<ABaseScene>();
        string path = Path.Combine(Application.dataPath, "..", "Assets", "ARCH_CONFIGURE", "AUTOGEN_EP.cs");
        string pathScenes = Path.Combine(Application.dataPath, "..", "Assets", "ARCH_CONFIGURE", "AUTOGEN_SCENES.cs");
        AddressableSceneGenerator.Generate(_scenes, pathScenes, force);
        EntryPointSOGenerator.Generate(_entrys, path, force);
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
    public static void Generate(List<Type> entryTypes, string outputPath, bool force = false)
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

        entryTypes.Sort((type1, type2) =>
        {
            var priority1 = type1.GetCustomAttribute<EntryPointAttribute>()?.Priority ?? int.MaxValue;
            var priority2 = type2.GetCustomAttribute<EntryPointAttribute>()?.Priority ?? int.MaxValue;
            return priority1.CompareTo(priority2);
        });

        if (!force && File.Exists(outputPath))
        {
            List<string> newServices = entryTypes.Select(x => x.Name).ToList();
            string pattern = @"(?<=\/\/)[A-Za-z]+";
            string text = File.ReadAllText(outputPath);

            MatchCollection matches = Regex.Matches(text, pattern);

            List<string> services = new List<string>();
            foreach (Match match in matches)
            {
                services.Add(match.Value);
            }
            bool FindNew = false;
            foreach (var service in newServices)
            {
                if (!services.Contains(service))
                    FindNew = true;
            }

            if (!FindNew)
                return;
        }

        var sb = new StringBuilder();

        // 2. Заголовок и using'и
        sb.AppendLine("// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.");
        sb.AppendLine("///RAW");
        foreach (var entry in entryTypes)
        {
            sb.AppendLine($"//{entry.Name}");

        }
        sb.AppendLine("///RAW");
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

        string firstTypeName = entryTypes.First().Name;
        sb.AppendLine($"        _firstStateType = typeof({firstTypeName});");
        sb.AppendLine(); // Пустая строка для читаемости

        // Регистрируем все состояния из списка
        foreach (var type in entryTypes)
        {
            if (type.GetCustomAttribute<EntryPointAttribute>()?.Ignore ?? true) continue;
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

            string code = sb.ToString();
            // Skip identical file to avoid needless recompilation
            if (File.Exists(outputPath) && File.ReadAllText(outputPath) == code)
                return;
            File.WriteAllText(outputPath, code);
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
    public static void Generate(List<Type> sceneTypes, string outputPath, bool force = false)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Путь для сохранения файла не может быть пустым.", nameof(outputPath));
        }

        sceneTypes ??= new List<Type>();

        if (!force && File.Exists(outputPath))
        {
            List<string> newServices = sceneTypes.Select(x => x.Name).ToList();
            string pattern = @"(?<=\/\/)[A-Za-z]+";
            string text = File.ReadAllText(outputPath);

            MatchCollection matches = Regex.Matches(text, pattern);

            List<string> services = new List<string>();
            foreach (Match match in matches)
            {
                services.Add(match.Value);
            }
            bool FindNew = false;
            foreach (var service in newServices)
            {
                if (!services.Contains(service))
                    FindNew = true;
            }

            if (!FindNew)
                return;
        }

        var sb = new StringBuilder();

        // 1. Заголовок и using'и
        sb.AppendLine("// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.");
        sb.AppendLine("///RAW");
        foreach (var entry in sceneTypes)
        {
            sb.AppendLine($"//{entry.Name}");

        }
        sb.AppendLine("///RAW");
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

            string code = sb.ToString();
            // Skip identical file to avoid needless recompilation
            if (File.Exists(outputPath) && File.ReadAllText(outputPath) == code)
                return;
            File.WriteAllText(outputPath, code);
            Debug.Log($"[AddressableSceneGenerator] Файл успешно сгенерирован: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AddressableSceneGenerator] Ошибка при записи файла: {ex.Message}");
        }
    }
}

#endif