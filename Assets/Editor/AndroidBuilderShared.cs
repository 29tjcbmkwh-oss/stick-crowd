// Toolchain/dependency-resolution/AAR-patching steps shared by AndroidBuilder.cs (debug APK)
// and ReleaseBuilder.cs (release AAB). Split out so the two build entry points can't drift.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AndroidBuilderShared
{
    public const string SdkRoot = "/Users/a/Library/Android/sdk";
    public const string NdkRoot = "/Users/a/Library/Android/sdk/ndk/21.3.6528147";
    public const string JdkRoot = "/Users/a/blue-vs-orange-runner/tools/jdk8/Contents/Home";

    public static void EnsureToolchain()
    {
        foreach (var p in new[] { SdkRoot, NdkRoot, JdkRoot })
            if (!Directory.Exists(p)) throw new Exception($"Missing toolchain dir: {p}");

        // Official API — identical to setting Preferences > External Tools by hand.
        // (Plain EditorPrefs writes proved to not persist / not be read by the SDK detector.)
        UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = SdkRoot;
        UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = NdkRoot;
        UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = JdkRoot;
        Debug.Log($"[AndroidBuilderShared] tool paths applied — sdk:{UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath} " +
                  $"ndk:{UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath} " +
                  $"jdk:{UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath}");

        // The above only wires Unity's OWN Android build step. EDM4U's dependency resolver
        // (PlayServicesResolver) shells out to its own bundled `gradlew` as a separate child
        // process to fetch Maven artifacts — that subprocess looks for Java via JAVA_HOME/PATH,
        // not via AndroidExternalToolsSettings, and fails with "Unable to locate a Java Runtime"
        // if the OS has no system-registered JDK (confirmed on this machine: `/usr/libexec/java_home`
        // finds nothing). Setting the process-level env var here means every child process this
        // Editor session spawns afterward (gradlew included) inherits a working JAVA_HOME.
        Environment.SetEnvironmentVariable("JAVA_HOME", JdkRoot);
        string jdkBin = Path.Combine(JdkRoot, "bin");
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Split(Path.PathSeparator).Contains(jdkBin))
            Environment.SetEnvironmentVariable("PATH", jdkBin + Path.PathSeparator + path);
        Debug.Log($"[AndroidBuilderShared] JAVA_HOME set for this process: {JdkRoot}");
    }

    // Sets Google's official TEST App IDs in the AdMob settings asset if none are set.
    // (The SDK's settings type is internal, so this goes through reflection.)
    // Before store release the team replaces these with the real AdMob App IDs via
    // Assets > Google Mobile Ads > Settings.
    public static void EnsureAdMobAppId()
    {
        const string testAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        const string testIosAppId     = "ca-app-pub-3940256099942544~1458002511";

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings"))
            .FirstOrDefault(t => t != null);
        if (type == null) { Debug.LogWarning("[AndroidBuilderShared] AdMob settings type not found — skipping App ID"); return; }

        var load = type.GetMethod("LoadInstance",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        var settings = load?.Invoke(null, null);
        if (settings == null) { Debug.LogWarning("[AndroidBuilderShared] could not load AdMob settings instance"); return; }

        var androidProp = type.GetProperty("GoogleMobileAdsAndroidAppId");
        var iosProp     = type.GetProperty("GoogleMobileAdsIOSAppId");
        if (string.IsNullOrEmpty((string)androidProp?.GetValue(settings)))
            androidProp?.SetValue(settings, testAndroidAppId);
        if (string.IsNullOrEmpty((string)iosProp?.GetValue(settings)))
            iosProp?.SetValue(settings, testIosAppId);

        EditorUtility.SetDirty((UnityEngine.Object)settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AndroidBuilderShared] AdMob App ID ensured (android: {(string)androidProp?.GetValue(settings)})");
    }

    // Mobile Ads 22.6 adds Android 13's <property> manifest node. Unity 2021.3.7
    // ships an older Android Gradle Plugin/AAPT combination that rejects that node,
    // even when compiling against API 33. Removing only this optional Privacy Sandbox
    // configuration keeps the full Ads SDK and rewarded-ad runtime available while
    // preserving compatibility with the project's current Unity toolchain.
    public static void PatchAdsLiteManifestForUnity2021()
    {
        const string assetPath = "Assets/Plugins/Android/com.google.android.gms.play-services-ads-lite-22.6.0.aar";
        const string propertyNode =
            "    <property android:name=\"android.adservices.AD_SERVICES_CONFIG\" android:resource=\"@xml/gma_ad_services_config\" />";

        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Resolved Google Mobile Ads Lite AAR was not found", fullPath);

        string xml;
        using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            ZipArchiveEntry manifest = archive.GetEntry("AndroidManifest.xml");
            if (manifest == null)
                throw new InvalidDataException("Google Mobile Ads Lite AAR has no AndroidManifest.xml");

            using (StreamReader reader = new StreamReader(manifest.Open(), Encoding.UTF8, true))
                xml = reader.ReadToEnd();
        }

        if (!xml.Contains(propertyNode))
        {
            Debug.Log("[AndroidBuilderShared] Ads Lite manifest is already compatible with Unity 2021");
            return;
        }

        xml = xml.Replace(propertyNode + "\r\n", string.Empty)
                 .Replace(propertyNode + "\n", string.Empty)
                 .Replace(propertyNode, string.Empty);

        // Do not use ZipArchiveMode.Update here. Its rewritten entry metadata is valid
        // for modern ZIP readers but Gradle 6.1's ExtractAarTransform misreads it as a
        // gigantic entry ("invalid entry size"). macOS's system zip produces the old,
        // conservative ZIP layout expected by Unity 2021's Android toolchain.
        string tempDirectory = Path.Combine(Path.GetTempPath(), "bvor-ads-lite-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(Path.Combine(tempDirectory, "AndroidManifest.xml"), xml, new UTF8Encoding(false));
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/zip",
                Arguments = $"-q \"{fullPath}\" AndroidManifest.xml",
                WorkingDirectory = tempDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception($"system zip failed while patching Ads Lite AAR (exit {process.ExitCode})");
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }

        AssetDatabase.ImportAsset(assetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Debug.Log("[AndroidBuilderShared] patched Ads Lite manifest for Unity 2021 Android tooling");
    }
}
