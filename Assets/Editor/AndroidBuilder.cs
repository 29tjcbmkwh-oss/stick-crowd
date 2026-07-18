// Headless Android build. Run:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod AndroidBuilder.BuildDebugApk
// Configures the external SDK/NDK/JDK paths (installed outside Unity Hub), forces IL2CPP
// + ARM64|ARMv7, pins target API to the installed platform, and produces Builds/StickCrowd.apk.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuilder
{
    private const string SdkRoot = "/Users/a/Library/Android/sdk";
    private const string NdkRoot = "/Users/a/Library/Android/sdk/ndk/21.3.6528147";
    private const string JdkRoot = "/Users/a/blue-vs-orange-runner/tools/jdk8/Contents/Home";
    // absolute: GUI-launched editors have an unpredictable working directory
    private const string ApkPath = "/Users/a/blue-vs-orange-runner/base/Builds/StickCrowd.apk";

    [MenuItem("Tools/Stickman/Build Debug APK")]
    public static void BuildDebugApk()
    {
        try
        {
            foreach (var p in new[] { SdkRoot, NdkRoot, JdkRoot })
                if (!Directory.Exists(p)) throw new Exception($"Missing toolchain dir: {p}");

            EnsureAdMobAppId();   // SDK's preprocessor hard-fails the build if App ID is empty

            // Official API — identical to setting Preferences > External Tools by hand.
            // (Plain EditorPrefs writes proved to not persist / not be read by the SDK detector.)
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = SdkRoot;
            UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = NdkRoot;
            UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = JdkRoot;
            Debug.Log($"[AndroidBuilder] tool paths applied — sdk:{UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath} " +
                      $"ndk:{UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath} " +
                      $"jdk:{UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath}");

            // EDM4U must materialise the transitive Android libraries before Unity builds.
            // The Google Mobile Ads Unity plugin's local AAR is only a bridge; without
            // play-services-ads the app compiles but fails at runtime with a missing
            // OnInitializationCompleteListener class.
            Debug.Log("[AndroidBuilder] resolving Google Android dependencies...");
            if (!GooglePlayServices.PlayServicesResolver.ResolveSync(true))
                throw new Exception("EDM4U failed to resolve the Google Mobile Ads Android dependencies");
            PatchAdsLiteManifestForUnity2021();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[AndroidBuilder] Google Android dependencies resolved");

            // store-capable runtime: IL2CPP + both ARM ABIs
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;   // pin to installed platform-33
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            // Unity 2021.3.7's bundled Swappy implementation can abort in
            // eglDestroySyncKHR on newer Android/emulator EGL drivers. It is an
            // optional frame-pacing layer, so keep it disabled until the project
            // is upgraded to a current Unity LTS. VSync still controls presentation.
            PlayerSettings.Android.optimizedFramePacing = false;
            // Unity 2021.3.7's Android player can leave a TelephonyManager
            // SecurityException pending when audio resumes after an external
            // activity (including a rewarded ad), then abort inside FMOD. Muting
            // other audio sources avoids the obsolete phone-call-listener path
            // without requesting READ_PHONE_STATE from players.
            PlayerSettings.muteOtherAudioSources = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Level.unity" },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development   // debug-signed: installs directly on a phone
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build {report.summary.result}: {report.summary.totalErrors} errors");
            // Unity 2021 can report Succeeded even when Android postprocess throws
            // ("Build target 'Android' not supported") — trust only the artifact.
            if (!File.Exists(ApkPath))
                throw new Exception($"BuildReport says Succeeded but no APK at {ApkPath} — check log for postprocess exceptions");

            Debug.Log($"[AndroidBuilder] SUCCESS — APK at {ApkPath} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalMinutes:F1} min)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuilder] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // Mobile Ads 22.6 adds Android 13's <property> manifest node. Unity 2021.3.7
    // ships an older Android Gradle Plugin/AAPT combination that rejects that node,
    // even when compiling against API 33. Removing only this optional Privacy Sandbox
    // configuration keeps the full Ads SDK and rewarded-ad runtime available while
    // preserving compatibility with the project's current Unity toolchain.
    private static void PatchAdsLiteManifestForUnity2021()
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
            Debug.Log("[AndroidBuilder] Ads Lite manifest is already compatible with Unity 2021");
            return;
        }

        xml = xml.Replace(propertyNode + "\r\n", string.Empty)
                 .Replace(propertyNode + "\n", string.Empty)
                 .Replace(propertyNode, string.Empty);

        // Do not use ZipArchiveMode.Update here. Its rewritten entry metadata is valid
        // for modern ZIP readers but Gradle 6.1's ExtractAarTransform misreads it as a
        // gigantic entry ("invalid entry size"). macOS's system zip produces the old,
        // conservative ZIP layout expected by Unity 2021's Android toolchain.
        string tempDirectory = Path.Combine(Path.GetTempPath(), "stickcrowd-ads-lite-" + Guid.NewGuid());
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
        Debug.Log("[AndroidBuilder] patched Ads Lite manifest for Unity 2021 Android tooling");
    }

    // Sets Google's official TEST App IDs in the AdMob settings asset if none are set.
    // (The SDK's settings type is internal, so this goes through reflection.)
    // Before store release the team replaces these with the real AdMob App IDs via
    // Assets > Google Mobile Ads > Settings.
    private static void EnsureAdMobAppId()
    {
        const string testAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        const string testIosAppId     = "ca-app-pub-3940256099942544~1458002511";

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings"))
            .FirstOrDefault(t => t != null);
        if (type == null) { Debug.LogWarning("[AndroidBuilder] AdMob settings type not found — skipping App ID"); return; }

        var load = type.GetMethod("LoadInstance",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        var settings = load?.Invoke(null, null);
        if (settings == null) { Debug.LogWarning("[AndroidBuilder] could not load AdMob settings instance"); return; }

        var androidProp = type.GetProperty("GoogleMobileAdsAndroidAppId");
        var iosProp     = type.GetProperty("GoogleMobileAdsIOSAppId");
        if (string.IsNullOrEmpty((string)androidProp?.GetValue(settings)))
            androidProp?.SetValue(settings, testAndroidAppId);
        if (string.IsNullOrEmpty((string)iosProp?.GetValue(settings)))
            iosProp?.SetValue(settings, testIosAppId);

        EditorUtility.SetDirty((UnityEngine.Object)settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AndroidBuilder] AdMob App ID ensured (android: {(string)androidProp?.GetValue(settings)})");
    }
}
