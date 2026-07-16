// Headless Android build. Run:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod AndroidBuilder.BuildDebugApk
// Configures the external SDK/NDK/JDK paths (installed outside Unity Hub), forces IL2CPP
// + ARM64|ARMv7, pins target API to the installed platform, and produces Builds/StickCrowd.apk.
using System;
using System.IO;
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

            // Official API — identical to setting Preferences > External Tools by hand.
            // (Plain EditorPrefs writes proved to not persist / not be read by the SDK detector.)
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = SdkRoot;
            UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = NdkRoot;
            UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = JdkRoot;
            Debug.Log($"[AndroidBuilder] tool paths applied — sdk:{UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath} " +
                      $"ndk:{UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath} " +
                      $"jdk:{UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath}");

            // store-capable runtime: IL2CPP + both ARM ABIs
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;   // pin to installed platform-33
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;

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
