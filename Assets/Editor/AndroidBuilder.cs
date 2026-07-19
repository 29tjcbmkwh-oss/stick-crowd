// Headless Android build. Run:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod AndroidBuilder.BuildDebugApk
// Configures the external SDK/NDK/JDK paths (installed outside Unity Hub), forces IL2CPP
// + ARM64|ARMv7, pins target API to the installed platform, and produces a debug-signed APK
// for quick device installs. For a signed release .aab see ReleaseBuilder.cs.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuilder
{
    // absolute: GUI-launched editors have an unpredictable working directory
    private const string ApkPath = "/Users/a/blue-vs-orange-runner/base/Builds/BlueVsOrangeRunner-debug.apk";

    [MenuItem("Tools/Stickman/Build Debug APK")]
    public static void BuildDebugApk()
    {
        try
        {
            AndroidBuilderShared.EnsureToolchain();
            AndroidBuilderShared.EnsureAdMobAppId();   // SDK's preprocessor hard-fails the build if App ID is empty

            // EDM4U must materialise the transitive Android libraries before Unity builds.
            // The Google Mobile Ads Unity plugin's local AAR is only a bridge; without
            // play-services-ads the app compiles but fails at runtime with a missing
            // OnInitializationCompleteListener class.
            Debug.Log("[AndroidBuilder] resolving Google Android dependencies...");
            if (!GooglePlayServices.PlayServicesResolver.ResolveSync(true))
                throw new Exception("EDM4U failed to resolve the Google Mobile Ads Android dependencies");
            AndroidBuilderShared.PatchAdsLiteManifestForUnity2021();
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
}
