// Visual overhaul pass: replaces the flat-green debug look with a gradient sky + fog,
// makes gate numbers large and readable on a contrasting plate, adds gate glow, and
// tunes lighting. Marker-driven (visual-overhaul-request) like the other setup steps.
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using _Scripts.Models;
using _Scripts.Utilities;

public static class VisualOverhaul
{
    private const string MatDir = "Assets/_Assets/Stickman/Materials";
    private static readonly Color SkyTop     = new Color(0.36f, 0.62f, 1.00f);
    private static readonly Color Horizon    = new Color(0.78f, 0.90f, 1.00f);
    private static readonly Color GroundTint = new Color(0.24f, 0.34f, 0.52f);
    private static readonly Color GateBlue   = BrandPalette.Blue;
    private static readonly Color GateOrange = BrandPalette.Orange;

    [MenuItem("Tools/Stickman/Apply Visual Overhaul")]
    public static void Run()
    {
        try
        {
            var plate = BuildPlateMaterial();
            ThemeGatesInPrefab("Assets/_Assets/ENV/Level1.prefab", plate);
            ThemeGatesInPrefab("Assets/_Assets/ENV/Corridor1.prefab", plate);
            OverhaulScene(plate);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualOverhaul] SUCCESS — gradient sky, readable gates, lighting applied.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VisualOverhaul] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // Dark rounded plate placed behind gate numbers so they read on any gate color.
    private static Material BuildPlateMaterial()
    {
        var path = $"{MatDir}/Mat_GatePlate.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(0.06f, 0.07f, 0.11f, 1f));
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.04f));
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void ThemeGatesInPrefab(string path, Material plate)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int n = BeautifyGates(root, plate);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[VisualOverhaul] {Path.GetFileName(path)}: {n} gates beautified");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static int BeautifyGates(GameObject root, Material plate)
    {
        int count = 0;

        // 1) glow on the gate slabs — MUST match ThemeSetup's corrected value, not override it.
        // This pass used to re-set emission to 0.55x after ThemeSetup had set 0.22x, silently
        // re-bleaching the gate hues whenever the two passes ran in sequence (the exact
        // emission/alpha stacking failure from the 2026-07-20 overnight lessons). Single
        // source of truth is ThemeSetup.GateEmission now.
        foreach (var corridor in root.GetComponentsInChildren<Corridor>(true))
        {
            var positive = corridor.GetCorridorType() is Constants.CorridorTypes.Increase
                                                       or Constants.CorridorTypes.Multiply;
            foreach (var r in corridor.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor",
                        (positive ? GateBlue : GateOrange) * ThemeSetup.GateEmission);
                    EditorUtility.SetDirty(mat);
                }
            }
            count++;
        }

        // 2) gate numbers: defer entirely to ThemeSetup's single authoritative label style
        // (flush on the panel, autosized inside a panel-clamped rect). This pass previously
        // applied its own competing billboard style, and whichever pass ran last won — the
        // root cause of the giant overlapping label smear in the 12:16 capture.
        foreach (var label in root.GetComponentsInChildren<TextMeshPro>(true))
        {
            if (label.gameObject.name != "GateLabel") continue;
            var corridor = label.GetComponentInParent<Corridor>();
            if (corridor == null) continue;

            label.text = ThemeSetup.GateLabelText(corridor);
            ThemeSetup.StyleGateLabel(label, corridor);

            // remove the backing plate from the earlier attempt
            var oldPlate = label.transform.Find("Plate");
            if (oldPlate != null) Object.DestroyImmediate(oldPlate.gameObject);
            EditorUtility.SetDirty(label);
        }

        return count;
    }

    private static void OverhaulScene(Material plate)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);

        // Visual Reskin Spec 2026-07-20: pale-blue-to-white gradient skybox (#DFF1FF ->
        // #FFFFFF), replacing the earlier vivid-blue procedural sky. Built-in procedural
        // skybox shader (guaranteed to build on mobile) tuned toward a soft, airy, almost-flat
        // gradient rather than a saturated "vivid sky" — the top viral crowd-runners read as
        // bright and toy-like, not scenic.
        var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Sky.mat");
        var proc = Shader.Find("Skybox/Procedural");
        if (sky == null || sky.shader != proc)
        {
            AssetDatabase.DeleteAsset($"{MatDir}/Mat_Sky.mat");
            sky = new Material(proc);
            AssetDatabase.CreateAsset(sky, $"{MatDir}/Mat_Sky.mat");
        }
        // Corrected sky (#1E7FE0 -> #7FC4FF, lesson #5): AtmosphereThickness 0.35 washed the
        // tint out to near-white in the actual capture regardless of _SkyTint — the procedural
        // shader needs enough scattering for the tint to render at all. Verified against
        // pixels, not the inspector swatch.
        sky.SetColor("_SkyTint", BrandPalette.SkyTop);
        sky.SetColor("_GroundColor", BrandPalette.SkyHorizon);
        sky.SetFloat("_AtmosphereThickness", 1.0f);
        sky.SetFloat("_SunSize", 0.01f);
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        // Only a whisper of far fog fading to white so the track edge disappears cleanly
        // instead of hard-clipping — kept minimal so it never reads as "murky."
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = BrandPalette.SkyHorizon;
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 200f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = BrandPalette.SkyTop;
        RenderSettings.ambientEquatorColor = new Color(0.93f, 0.95f, 0.98f);
        // Bright ground bounce (was a dark 0.42/0.46/0.54) — a dark ambient-ground term was
        // muddying the underside of the crowd against the new light floor.
        RenderSettings.ambientGroundColor = BrandPalette.GroundLight;

        // sun
        var sun = Object.FindObjectsOfType<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }

        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
            cam.clearFlags = CameraClearFlags.Skybox;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[VisualOverhaul] scene: sky + fog + lighting");
    }
}
