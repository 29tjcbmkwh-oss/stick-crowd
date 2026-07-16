// Imports the Google Mobile Ads SDK package and enables the ADMOB_ENABLED define so
// AdMobService compiles in. Marker-driven like the other setup steps:
//   import-admob-request  -> import package + add define (build separately afterwards)
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AdMobImporter
{
    private const string Marker  = "/Users/a/blue-vs-orange-runner/base/import-admob-request";
    private const string Package = "/private/tmp/claude-501/-Users-a-Pictures/1323ad1f-8a16-4732-bce2-307e1c67d67a/scratchpad/GoogleMobileAds-v8.7.0.unitypackage";

    static AdMobImporter()
    {
        EditorApplication.delayCall += () =>
        {
            // Self-healing: the package import's domain reload destroys in-memory callbacks,
            // so the define is applied by this startup check instead (idempotent).
            if (AssetDatabase.IsValidFolder("Assets/GoogleMobileAds"))
            {
                var before = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
                AddDefine(BuildTargetGroup.Android);
                AddDefine(BuildTargetGroup.iOS);
                if (!before.Contains("ADMOB_ENABLED"))
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log("[AdMobImporter] SUCCESS — SDK present, ADMOB_ENABLED set + saved for Android+iOS");
                }
            }

            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            Run();
        };
    }

    [MenuItem("Tools/Stickman/Import AdMob SDK")]
    public static void Run()
    {
        if (!File.Exists(Package))
        {
            Debug.LogError($"[AdMobImporter] package not found at {Package}");
            return;
        }
        Debug.Log("[AdMobImporter] importing Google Mobile Ads SDK...");
        AssetDatabase.ImportPackage(Package, false);   // non-interactive
        AssetDatabase.importPackageCompleted += name =>
        {
            AddDefine(BuildTargetGroup.Android);
            AddDefine(BuildTargetGroup.iOS);
            AssetDatabase.SaveAssets();
            Debug.Log("[AdMobImporter] SUCCESS — SDK imported, ADMOB_ENABLED set for Android+iOS");
        };
    }

    private static void AddDefine(BuildTargetGroup group)
    {
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        if (!defines.Contains("ADMOB_ENABLED"))
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
                string.IsNullOrEmpty(defines) ? "ADMOB_ENABLED" : defines + ";ADMOB_ENABLED");
    }
}
