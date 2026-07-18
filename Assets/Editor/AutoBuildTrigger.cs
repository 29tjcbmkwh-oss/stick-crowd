// Runs marker-requested work from a GUI editor without menu interaction. Markers at the
// project root:
//   apply-theme-request   -> run the Blue-vs-Orange theme pass
//   autobuild-request     -> build the Android APK
//
// Uses EditorApplication.update (re-subscribed on every domain reload) rather than
// delayCall: importing the AdMob SDK / EDM4U triggers repeated domain reloads that DISCARD
// pending delayCalls before they fire, so the build never started. Polling update survives
// reloads and fires only once the editor is idle (not compiling/updating).
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    private const string BuildMarker   = "/Users/a/blue-vs-orange-runner/base/autobuild-request";
    private const string ThemeMarker   = "/Users/a/blue-vs-orange-runner/base/apply-theme-request";
    private const string VisualMarker  = "/Users/a/blue-vs-orange-runner/base/visual-overhaul-request";
    private static bool _fired;

    static AutoBuildTrigger()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (_fired) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        var theme  = File.Exists(ThemeMarker);
        var visual = File.Exists(VisualMarker);
        var build  = File.Exists(BuildMarker);
        if (!theme && !visual && !build) return;

        _fired = true;
        EditorApplication.update -= Tick;

        if (theme)
        {
            File.Delete(ThemeMarker);
            Debug.Log("[AutoBuildTrigger] theme marker found — applying theme");
            ThemeSetup.Run();
        }
        if (visual)
        {
            File.Delete(VisualMarker);
            Debug.Log("[AutoBuildTrigger] visual marker found — applying visual overhaul");
            VisualOverhaul.Run();
        }
        if (build)
        {
            File.Delete(BuildMarker);
            Debug.Log("[AutoBuildTrigger] build marker found — starting Android build");
            AndroidBuilder.BuildDebugApk();
        }
    }
}
