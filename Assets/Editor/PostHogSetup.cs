// Enables the POSTHOG_ENABLED define once the official posthog-unity UPM package (git URL
// dependency in Packages/manifest.json) has actually resolved, so PostHogAnalyticsService
// compiles in. Same self-healing startup-check pattern as AdMobImporter: no marker needed —
// the manifest entry is the request, this just flips the define when the package is real.
// If the git dependency ever fails to resolve, the define stays off and the game keeps
// running on the log-only analytics service; nothing breaks.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PostHogSetup
{
    private const string Define = "POSTHOG_ENABLED";

    static PostHogSetup()
    {
        EditorApplication.delayCall += () =>
        {
            var cache = "/Users/a/blue-vs-orange-runner/base/Library/PackageCache";
            bool resolved = Directory.Exists(cache) &&
                            Directory.GetDirectories(cache, "com.posthog.unity*").Any();
            if (!resolved) return;

            var before = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            AddDefine(BuildTargetGroup.Android);
            AddDefine(BuildTargetGroup.iOS);
            if (!before.Contains(Define))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[PostHogSetup] SUCCESS — posthog-unity package resolved, {Define} set + saved for Android+iOS");
            }
        };
    }

    private static void AddDefine(BuildTargetGroup group)
    {
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        if (defines.Contains(Define)) return;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
            string.IsNullOrEmpty(defines) ? Define : defines + ";" + Define);
    }
}
