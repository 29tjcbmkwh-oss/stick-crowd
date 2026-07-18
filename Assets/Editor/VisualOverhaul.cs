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
    private static readonly Color GateBlue   = new Color(0.20f, 0.50f, 1.00f);
    private static readonly Color GateRed    = new Color(1.00f, 0.28f, 0.30f);

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

        // 1) glow on the gate slabs
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
                    mat.SetColor("_EmissionColor", (positive ? GateBlue : GateRed) * 0.35f);
                    EditorUtility.SetDirty(mat);
                }
            }
            count++;
        }

        // 2) big readable numbers on a plate, standing up and facing the camera
        foreach (var label in root.GetComponentsInChildren<TextMeshPro>(true))
        {
            if (label.gameObject.name != "GateLabel") continue;
            var corridor = label.GetComponentInParent<Corridor>();
            if (corridor == null) continue;

            label.text = GateText(corridor);
            label.fontSize = 6;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.32f;               // thick dark outline = readable on any gate
            label.outlineColor = new Color32(6, 8, 14, 255);
            label.enableWordWrapping = false;

            var t = label.transform;
            t.localPosition = new Vector3(0f, 0.75f, 0f);   // sit on the gate slab, not floating above
            t.localRotation = Quaternion.identity;          // Billboard handles facing
            t.localScale = Vector3.one;
            var rt = label.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(4.5f, 2f);

            if (label.GetComponent<_Scripts.Core.Billboard>() == null)
                label.gameObject.AddComponent<_Scripts.Core.Billboard>();

            // remove the backing plate from the earlier attempt (it culls/renders wrong once billboarded)
            var oldPlate = label.transform.Find("Plate");
            if (oldPlate != null) Object.DestroyImmediate(oldPlate.gameObject);
        }

        return count;
    }

    private static string GateText(Corridor c) => c.GetCorridorType() switch
    {
        Constants.CorridorTypes.Increase => $"+{c.increaseAmount}",
        Constants.CorridorTypes.Decrease => $"-{c.decreaseAmount}",
        Constants.CorridorTypes.Multiply => $"x{c.multiplyAmount}",
        Constants.CorridorTypes.Divide   => $"/{c.divideAmount}",
        _ => "?"
    };

    private static void OverhaulScene(Material plate)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);

        // gradient procedural sky (kills the flat green)
        var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Sky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, $"{MatDir}/Mat_Sky.mat");
        }
        sky.SetColor("_SkyTint", SkyTop);
        sky.SetColor("_GroundColor", GroundTint);
        sky.SetFloat("_SunSize", 0.04f);
        sky.SetFloat("_AtmosphereThickness", 1.1f);
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Horizon;
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 95f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 1.00f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.60f, 0.72f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.33f, 0.42f);

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
