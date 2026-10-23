#if HAS_VOODYA
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Runs the checks of the Arch Config window before every build and fails the build on errors,
/// so problems like a missing scene reference are found before the app is shipped.
/// </summary>
public class ArchBuildValidator : IPreprocessBuildWithReport
{
    private const string ENABLED_PREF = "VooArch.ValidateOnBuild";
    private const string MENU = "VooArch/Validate Config On Build";

    public int callbackOrder => 0;

    private static bool Enabled => EditorPrefs.GetBool(ENABLED_PREF, true);

    [MenuItem(MENU)]
    private static void Toggle() => EditorPrefs.SetBool(ENABLED_PREF, !Enabled);

    [MenuItem(MENU, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MENU, Enabled);
        return true;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!Enabled) return;

        VooConfigSync.Sync();
        var model = new ArchConfigModel();
        model.Reload(createMissing: false);

        foreach (var warning in model.Issues.Where(i => i.Type == MessageType.Warning))
            Debug.LogWarning($"[VooArch] {warning.Text}");

        var errors = model.Issues.Where(i => i.Type == MessageType.Error).Select(i => i.Text).ToList();
        if (errors.Count == 0) return;

        throw new BuildFailedException(
            "VooArch config has errors, fix them in VooArch/Voo arch configure " +
            "(or disable VooArch/Validate Config On Build):\n- " + string.Join("\n- ", errors));
    }
}
#endif
