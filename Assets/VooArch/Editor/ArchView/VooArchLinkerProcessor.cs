#if HAS_VOODYA
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using Voodya.Core;

/// <summary>
/// Entry points and scenes are created from type names stored in configs,
/// so the managed code stripper can't see them in code. This keeps them in IL2CPP builds.
/// </summary>
public class VooArchLinkerProcessor : IUnityLinkerProcessor
{
    public int callbackOrder => 0;

    public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
    {
        var types = TypeCache.GetTypesDerivedFrom<ABaseEntryPoint>()
            .Concat(TypeCache.GetTypesDerivedFrom<ABaseScene>())
            .Where(t => !t.IsAbstract && !VooConfigSync.IsTestType(t))
            .Append(typeof(DefaultState<>))
            .Distinct();

        var xml = new StringBuilder("<linker>\n");
        foreach (var assembly in types.GroupBy(t => t.Assembly.GetName().Name))
        {
            xml.AppendLine($"  <assembly fullname=\"{SecurityElement.Escape(assembly.Key)}\">");
            foreach (var type in assembly)
                xml.AppendLine($"    <type fullname=\"{SecurityElement.Escape(LinkerName(type))}\" preserve=\"all\"/>");
            xml.AppendLine("  </assembly>");
        }
        xml.AppendLine("</linker>");

        string path = Path.Combine(Path.GetFullPath("Temp"), "VooArchLink.xml");
        File.WriteAllText(path, xml.ToString());
        return path;
    }

    // link.xml uses '/' for nested types
    private static string LinkerName(Type type) => type.FullName.Replace('+', '/');
}
#endif
