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
        versions[2]++;
        string newVersion = "";
        for (int i = 0; i < versions.Count; i++)
        {
            newVersion += versions[i].ToString() + ".";
        }
        newVersion += "alpha";
        Debug.LogError($"New version is {newVersion}");
        PlayerSettings.bundleVersion = newVersion;
    }
}
