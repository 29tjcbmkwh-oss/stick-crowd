// Runs once the editor finishes loading scripts. Marker files at the project root let an
// external process (or a teammate/agent) request work from a GUI editor without menu
// interaction — the next domain reload / editor focus consumes them:
//   autobuild-request     -> build the Android APK
//   apply-theme-request   -> run the Blue-vs-Orange theme pass
// Theme runs before build when both markers are present.
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    private const string BuildMarker = "/Users/a/blue-vs-orange-runner/base/autobuild-request";
    private const string ThemeMarker = "/Users/a/blue-vs-orange-runner/base/apply-theme-request";

    static AutoBuildTrigger()
    {
        if (!File.Exists(BuildMarker) && !File.Exists(ThemeMarker)) return;
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(ThemeMarker))
            {
                File.Delete(ThemeMarker);
                Debug.Log("[AutoBuildTrigger] theme marker found — applying theme");
                ThemeSetup.Run();
            }
            if (File.Exists(BuildMarker))
            {
                File.Delete(BuildMarker);
                Debug.Log("[AutoBuildTrigger] build marker found — starting Android build");
                AndroidBuilder.BuildDebugApk();
            }
        };
    }
}
