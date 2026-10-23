using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class PostBuild
{

    [PostProcessBuildAttribute()]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        Debug.Log(pathToBuiltProject);
        string rewVersion = PlayerSettings.bundleVersion;
        List<int> versions = rewVersion.Split('.').ToList().Where(x => int.TryParse(x, out int y)).Select(x => int.Parse(x)).ToList();
        // Versions like "1.0" have no patch number yet
        while (versions.Count < 3)
            versions.Add(0);
        versions[2]++;
        string newVersion = "";
        for (int i = 0; i < versions.Count; i++)
        {
            newVersion += versions[i].ToString() + ".";
        }
        newVersion += "alpha";
        Debug.Log($"[VooArch] New version is {newVersion}");
        PlayerSettings.bundleVersion = newVersion;
    }
}
