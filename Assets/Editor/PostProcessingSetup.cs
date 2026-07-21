// Visual Reskin Spec §3: Bloom, Color Adjustments, Vignette, Ambient Occlusion, ACES
// tonemapping. Built-in RP project -> Post-Processing Stack v2 (com.unity.postprocessing).
// Marker-driven (post-processing-request) like the other setup steps in this file's family.
//
// BUG FOUND 2026-07-21: the first version called profile.AddSettings<T>() and saved, which
// produced a profile whose `settings:` list was four NULL entries ({fileID: 0}) — i.e. NO
// bloom at all, so the gates' emission had nothing to halo it. PostProcessEffectSettings are
// ScriptableObjects; adding them to a profile at edit time does NOT persist them unless they
// are also written into the profile asset via AssetDatabase.AddObjectToAsset. Fixed below,
// and the profile is rebuilt from scratch each run so a previously-corrupted asset self-heals
// rather than silently staying broken.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class PostProcessingSetup
{
    private const string ProfilePath = "Assets/_Assets/Stickman/Materials/PPv2_MainProfile.asset";

    [MenuItem("Tools/Stickman/Apply Post-Processing")]
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);

            // Rebuild from scratch: guarantees we never inherit the null-settings corruption.
            AssetDatabase.DeleteAsset(ProfilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = AddPersisted<Bloom>(profile);
            bloom.threshold.overrideState = true;  bloom.threshold.value = 1.0f;
            bloom.intensity.overrideState = true;  bloom.intensity.value = 0.4f;
            bloom.softKnee.overrideState = true;   bloom.softKnee.value = 0.5f;
            bloom.dirtIntensity.overrideState = true; bloom.dirtIntensity.value = 0f; // no lens-dirt asset

            var grading = AddPersisted<ColorGrading>(profile);
            grading.tonemapper.overrideState = true; grading.tonemapper.value = Tonemapper.ACES;
            grading.contrast.overrideState = true;   grading.contrast.value = 10f;
            grading.saturation.overrideState = true; grading.saturation.value = 14f;

            var vignette = AddPersisted<Vignette>(profile);
            vignette.intensity.overrideState = true;  vignette.intensity.value = 0.2f;
            vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.4f;

            var ao = AddPersisted<AmbientOcclusion>(profile);
            ao.intensity.overrideState = true; ao.intensity.value = 0.3f;
            // ScalableAmbientObscurance is the Built-in RP compatible mode (MSVO needs deferred).
            ao.mode.overrideState = true; ao.mode.value = AmbientOcclusionMode.ScalableAmbientObscurance;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);

            EnsureVolume(profile);
            int cams = EnsureCameraLayer();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Verify rather than assume: re-load the saved asset and count non-null settings.
            var saved = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            int live = saved == null ? 0 : saved.settings.Count(s => s != null);
            if (live != 4)
                throw new System.Exception($"profile persisted {live}/4 effects — expected 4 (bloom/grading/vignette/AO)");

            Debug.Log($"[PostProcessingSetup] SUCCESS — {live}/4 effects persisted (Bloom+ACES+Vignette+AO), " +
                      $"PostProcessLayer on {cams} camera(s).");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PostProcessingSetup] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // The critical fix: create the effect, add it to the profile, AND write it into the
    // profile asset file as a sub-object so it survives the save.
    private static T AddPersisted<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        T settings = profile.AddSettings<T>();
        settings.active = true;
        settings.enabled.overrideState = true;
        settings.enabled.value = true;
        settings.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(settings, profile);
        return settings;
    }

    // Global volume so it always applies to the main camera regardless of the crowd/camera's
    // exact position on the track (a local volume would need per-level bounds tuning).
    private static void EnsureVolume(PostProcessProfile profile)
    {
        GameObject go = GameObject.Find("PostProcessVolume");
        if (go == null) go = new GameObject("PostProcessVolume");

        PostProcessVolume volume = go.GetComponent<PostProcessVolume>();
        if (volume == null) volume = go.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.profile = profile;
        volume.priority = 0;
        volume.weight = 1f;

        int ppLayer = LayerMask.NameToLayer("PostProcessing");
        if (ppLayer >= 0) go.layer = ppLayer; // otherwise leave on Default; mask below covers it
        EditorUtility.SetDirty(go);
    }

    private static int EnsureCameraLayer()
    {
        int count = 0;
        foreach (Camera cam in Object.FindObjectsOfType<Camera>(true))
        {
            PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();
            if (layer == null) layer = cam.gameObject.AddComponent<PostProcessLayer>();

            int ppLayerIndex = LayerMask.NameToLayer("PostProcessing");
            // Fall back to "everything" when the dedicated layer was never created in this
            // project — otherwise the volume sits on Default and the layer never sees it.
            layer.volumeLayer = ppLayerIndex >= 0 ? (LayerMask)(1 << ppLayerIndex) : (LayerMask)~0;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

            // PostProcessLayer needs its internal resources asset assigned or it renders nothing.
            EnsureResources(layer);

            EditorUtility.SetDirty(cam.gameObject);
            count++;
        }
        return count;
    }

    // PostProcessLayer.m_Resources is required; when the component is added from script it is
    // left null and post-processing silently does nothing. Find the package's resources asset.
    private static void EnsureResources(PostProcessLayer layer)
    {
        var so = new SerializedObject(layer);
        var prop = so.FindProperty("m_Resources");
        if (prop == null || prop.objectReferenceValue != null) return;

        string guid = AssetDatabase.FindAssets("t:PostProcessResources").FirstOrDefault();
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning("[PostProcessingSetup] PostProcessResources asset not found — post-processing may not render");
            return;
        }
        var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guid));
        prop.objectReferenceValue = res;
        so.ApplyModifiedProperties();
    }
}
