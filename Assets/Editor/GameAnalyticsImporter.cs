// Imports the GameAnalytics Unity SDK package and enables the GAMEANALYTICS_ENABLED define
// so GameAnalyticsService compiles in. Mirrors AdMobImporter exactly. Marker-driven:
//   import-gameanalytics-request -> import package + add define
// The .unitypackage path below is where the SDK should be dropped once obtained from the
// official GameAnalytics GitHub releases; after import, the game key + secret still must be
// entered in the GA Settings asset (Window > GameAnalytics > Select Settings) — dashboard
// credentials are Ali's step, the code path is ready without them.
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameAnalyticsImporter
{
    private const string Marker  = "/Users/a/blue-vs-orange-runner/base/import-gameanalytics-request";
    private const string Package = "/Users/a/blue-vs-orange-runner/base/Builds/GA_SDK_UNITY.unitypackage";
    private const string Define  = "GAMEANALYTICS_ENABLED";

    static GameAnalyticsImporter()
    {
        EditorApplication.delayCall += () =>
        {
            // Self-healing like AdMobImporter: the import's domain reload destroys in-memory
            // callbacks, so the define is applied by this idempotent startup check instead.
            if (AssetDatabase.IsValidFolder("Assets/GameAnalytics"))
            {
                var before = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
                AddDefine(BuildTargetGroup.Android);
                AddDefine(BuildTargetGroup.iOS);
                if (!before.Contains(Define))
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[GameAnalyticsImporter] SUCCESS — SDK present, {Define} set + saved for Android+iOS");
                }
            }

            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            if (!File.Exists(Package))
            {
                Debug.LogError($"[GameAnalyticsImporter] FAILED — no package at {Package}. " +
                               "Download GA_SDK_UNITY.unitypackage from the official GameAnalytics GitHub releases first.");
                return;
            }
            Debug.Log("[GameAnalyticsImporter] importing GameAnalytics SDK package…");
            AssetDatabase.ImportPackage(Package, false);
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
