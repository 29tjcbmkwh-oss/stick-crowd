// Headless Android *release* build. Run:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod ReleaseBuilder.BuildReleaseAab
// Produces a signed, non-development release .aab (App Bundle) for Play Console upload.
// Reuses AndroidBuilder's toolchain wiring (SDK/NDK/JDK paths, EDM4U dependency resolution,
// the Ads-Lite manifest patch) but configures release signing + strips debug symbols/dev flags.
//
// Signing credentials are read from Builds/keystore.properties (gitignored — never commit it).
// That file does not exist until someone runs keytool locally; see
// Assets/Editor/ReleaseBuilder.cs's KeystorePropertiesPath for the expected format:
//   storeFile=Builds/arcfield-release.jks
//   storePassword=...
//   keyAlias=...
//   keyPassword=...
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ReleaseBuilder
{
    private const string KeystorePropertiesPath = "Builds/keystore.properties";
    private const string AabPath = "/Users/a/blue-vs-orange-runner/base/Builds/BlueVsOrangeRunner.aab";

    [MenuItem("Tools/Stickman/Build Release AAB")]
    public static void BuildReleaseAab()
    {
        try
        {
            AndroidBuilderShared.EnsureToolchain();
            AndroidBuilderShared.EnsureAdMobAppId();

            Debug.Log("[ReleaseBuilder] resolving Google Android dependencies...");
            if (!GooglePlayServices.PlayServicesResolver.ResolveSync(true))
                throw new Exception("EDM4U failed to resolve the Google Mobile Ads Android dependencies");
            AndroidBuilderShared.PatchAdsLiteManifestForUnity2021();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[ReleaseBuilder] Google Android dependencies resolved");

            ApplySigning();

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.optimizedFramePacing = false; // see AndroidBuilder.cs for why
            PlayerSettings.muteOtherAudioSources = true;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;

            Directory.CreateDirectory(Path.GetDirectoryName(AabPath)!);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Level.unity" },
                locationPathName = AabPath,
                target = BuildTarget.Android,
                options = BuildOptions.None // release: no Development/AutoConnectProfiler/AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build {report.summary.result}: {report.summary.totalErrors} errors");
            if (!File.Exists(AabPath))
                throw new Exception($"BuildReport says Succeeded but no AAB at {AabPath} — check log for postprocess exceptions");

            Debug.Log($"[ReleaseBuilder] SUCCESS — AAB at {AabPath} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalMinutes:F1} min)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ReleaseBuilder] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    private static void ApplySigning()
    {
        string fullPropsPath = Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath)!, KeystorePropertiesPath);
        if (!File.Exists(fullPropsPath))
            throw new FileNotFoundException(
                $"No release signing config at {KeystorePropertiesPath}. Generate a keystore " +
                "(keytool -genkeypair ...) and write its credentials there before building a " +
                "release AAB — see the header comment in ReleaseBuilder.cs for the file format.",
                fullPropsPath);

        var props = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(fullPropsPath))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
            int eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            props[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
        }

        string storeFileRelative = props["storeFile"];
        string storeFileFull = Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath)!, storeFileRelative);
        if (!File.Exists(storeFileFull))
            throw new FileNotFoundException("Release keystore referenced by keystore.properties not found", storeFileFull);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = storeFileFull;
        PlayerSettings.Android.keystorePass = props["storePassword"];
        PlayerSettings.Android.keyaliasName = props["keyAlias"];
        PlayerSettings.Android.keyaliasPass = props["keyPassword"];

        Debug.Log($"[ReleaseBuilder] release signing applied (alias: {props["keyAlias"]})");
    }
}
