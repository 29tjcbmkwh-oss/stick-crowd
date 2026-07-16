// Runs once the editor finishes loading scripts. If a marker file exists at the project
// root, consumes it and kicks off the Android build — lets an external process request a
// build from a GUI editor without menu interaction (e.g. when the display is asleep).
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    private const string Marker = "/Users/a/blue-vs-orange-runner/base/autobuild-request";

    static AutoBuildTrigger()
    {
        if (!File.Exists(Marker)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Marker)) return;   // domain reloads re-enter here; only fire once
            File.Delete(Marker);
            Debug.Log("[AutoBuildTrigger] build marker found — starting Android build");
            AndroidBuilder.BuildDebugApk();
        };
    }
}
