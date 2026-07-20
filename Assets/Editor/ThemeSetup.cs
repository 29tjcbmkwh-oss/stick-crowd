// Blue-vs-Orange theme pass: transforms the placeholder-gray crowd-runner into the
// reference look (TikTok stickman genre): vivid green void, dark track, red obstacle
// bars, blue/red labeled gates, styled UI, proper starting crowd.
// Run: Tools > Stickman > Apply Theme   (or via autobuild-style marker if wired)
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using _Scripts.Models;
using _Scripts.Utilities;
// RadialFormation & FormationBase live in the global namespace (no using needed)

public static class ThemeSetup
{
    // ---- palette (from the reference clip; gate colors come from the shared brand tokens) ----
    private static readonly Color VoidGreen   = new Color(0.13f, 0.78f, 0.29f);  // bright flat green background
    private static readonly Color TrackDark   = new Color(0.16f, 0.14f, 0.19f);  // near-black purple track
    private static readonly Color WallDark    = new Color(0.24f, 0.21f, 0.29f);
    private static readonly Color ObstacleRed = new Color(0.95f, 0.23f, 0.25f);  // static hazard prop, not a gate choice
    private static readonly Color GateBlue    = BrandPalette.BlueTranslucent;
    private static readonly Color GateOrange  = BrandPalette.OrangeTranslucent;
    private const string MatDir = "Assets/_Assets/Stickman/Materials";

    [MenuItem("Tools/Stickman/Apply Theme")]
    public static void Run()
    {
        try
        {
            var trackMat = Opaque("Mat_TrackDark", TrackDark);
            var wallMat  = Opaque("Mat_WallDark", WallDark);
            var obstMat  = Opaque("Mat_ObstacleRed", ObstacleRed);
            var gateBlue   = Transparent("Mat_GateBlue", GateBlue);
            var gateOrange = Transparent("Mat_GateOrange", GateOrange);

            ThemePrefab("Assets/_Assets/ENV/Level1.prefab", trackMat, wallMat, obstMat, gateBlue, gateOrange);
            ThemePrefab("Assets/_Assets/ENV/Corridor1.prefab", trackMat, wallMat, obstMat, gateBlue, gateOrange);
            FixCatRotationTarget();
            ThemeScene(trackMat, wallMat, obstMat, gateBlue, gateOrange);

            AssetDatabase.SaveAssets();
            Debug.Log("[ThemeSetup] SUCCESS — Blue-vs-Orange theme applied.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThemeSetup] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // ---------- materials ----------
    private static Material Opaque(string name, Color c)
    {
        var path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Transparent(string name, Color c)
    {
        var m = Opaque(name, c);
        // Standard shader transparent setup
        m.SetFloat("_Mode", 3);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- shared theming of a hierarchy ----------
    private static int ThemeHierarchy(GameObject root, Material track, Material wall,
                                      Material obst, Material gateBlue, Material gateOrange)
    {
        int touched = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var n = r.gameObject.name.ToLowerInvariant();
            var corridor = r.GetComponent<Corridor>();
            if (corridor != null)
            {
                var positive = corridor.GetCorridorType() is Constants.CorridorTypes.Increase
                                                           or Constants.CorridorTypes.Multiply;
                r.sharedMaterials = Enumerable.Repeat(positive ? gateBlue : gateOrange,
                                                      r.sharedMaterials.Length).ToArray();
                EnsureGateLabel(corridor);
                touched++;
            }
            else if (n.Contains("obstacle"))
            { r.sharedMaterials = Enumerable.Repeat(obst, r.sharedMaterials.Length).ToArray(); touched++; }
            else if (n.Contains("ground") || n.Contains("floor") || n.Contains("road") || n.Contains("levelend"))
            { r.sharedMaterials = Enumerable.Repeat(track, r.sharedMaterials.Length).ToArray(); touched++; }
            else if (n.Contains("wall"))
            { r.sharedMaterials = Enumerable.Repeat(wall, r.sharedMaterials.Length).ToArray(); touched++; }
        }
        return touched;
    }

    private static void EnsureGateLabel(Corridor c)
    {
        const string labelName = "GateLabel";
        string text = c.GetCorridorType() switch
        {
            Constants.CorridorTypes.Increase => $"+{c.increaseAmount}",
            Constants.CorridorTypes.Decrease => $"-{c.decreaseAmount}",
            Constants.CorridorTypes.Multiply => $"x{c.multiplyAmount}",
            Constants.CorridorTypes.Divide   => $"÷{c.divideAmount}",
            _ => "?"
        };

        // Scene corridors are prefab instances — children inherited from the prefab cannot be
        // destroyed there. Update an existing label in place; only create when absent.
        var existing = c.transform.Find(labelName);
        if (existing != null)
        {
            var existingTmp = existing.GetComponent<TextMeshPro>();
            if (existingTmp != null) { existingTmp.text = text; EditorUtility.SetDirty(existingTmp); }
            return;
        }

        var go = new GameObject(labelName);
        go.transform.SetParent(c.transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.15f;
        // gate panels lie flat-ish; float the label above the panel, facing the camera (which
        // looks down the -Z of the track from behind the player → face text back up the track)
        go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = Vector3.one * 0.12f;
        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20, 6);
    }

    private static void ThemePrefab(string path, Material track, Material wall,
                                    Material obst, Material gateBlue, Material gateOrange)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var n = ThemeHierarchy(root, track, wall, obst, gateBlue, gateOrange);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[ThemeSetup] {Path.GetFileName(path)}: {n} renderers themed");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------- scene: camera, crowd size, UI ----------
    private static void ThemeScene(Material track, Material wall, Material obst,
                                   Material gateBlue, Material gateOrange)
    {
        var scenePath = "Assets/Scenes/Level.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // vivid flat void
        foreach (var cam in UnityEngine.Object.FindObjectsOfType<Camera>(true))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = VoidGreen;
        }

        // proper starting crowd
        foreach (var f in UnityEngine.Object.FindObjectsOfType<RadialFormation>(true))
            if (f.amount < 3) f.amount = 3;

        // theme anything placed directly in the scene (not via prefabs)
        foreach (var rootGo in scene.GetRootGameObjects())
            ThemeHierarchy(rootGo, track, wall, obst, gateBlue, gateOrange);

        StyleUI();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ThemeSetup] scene themed: camera void, crowd size, UI");
    }

    // Steering rotates Cat.rotationTarget — must be the stickman rig (the original
    // capsule-era reference was lost in the character swap and threw at runtime).
    private static void FixCatRotationTarget()
    {
        const string path = "Assets/_Assets/PLAYER/ExampleCat.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var cat = root.GetComponent<Cat>();
            var rig = root.transform.Find("StickmanRig");
            if (cat != null && rig != null && cat.rotationTarget != rig)
            {
                cat.rotationTarget = rig;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[ThemeSetup] Cat.rotationTarget -> StickmanRig");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Replace the template's placeholder-white UI with the Blue-vs-Orange skin.
    private static void StyleUI()
    {
        var uiBlue   = new Color(0.20f, 0.45f, 1.00f);
        var uiOrange = new Color(1.00f, 0.55f, 0.10f);
        var dim      = new Color(0f, 0f, 0f, 0.45f);        // translucent backdrop
        var chipDark = new Color(0.10f, 0.09f, 0.13f, 0.85f); // HUD chips

        foreach (var img in UnityEngine.Object.FindObjectsOfType<Image>(true))
        {
            var n = img.gameObject.name.ToLowerInvariant();
            var isButton = n.Contains("button") || n.Contains("restart") || n.Contains("start");
            if (n.Contains("panelback") || n.Contains("allpanels"))
                img.color = dim;                              // was solid white, hid the game
            else if (isButton)
                img.color = uiBlue;
            else if (n.Contains("progressbar") || n.Contains("bar"))
                img.color = n.Contains("back") ? chipDark : uiOrange;
            else if (n.Contains("levelback") || n.Contains("count") || n.Contains("score") || n == "image")
                img.color = chipDark;
            // Catch-all: any leftover near-white placeholder panel (e.g. the score chip that
            // rendered white-on-white on device) becomes a dark HUD chip.
            else if (img.color.r > 0.85f && img.color.g > 0.85f && img.color.b > 0.85f)
                img.color = chipDark;
            EditorUtility.SetDirty(img);
        }

        foreach (var t in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true))
        {
            var n = t.gameObject.name.ToLowerInvariant();
            t.fontStyle = FontStyles.Bold;
            if (n.Contains("tap"))       { t.text = "TAP TO PLAY"; t.color = Color.white; }
            else if (n.Contains("level")) t.color = Color.white;
            else if (n.Contains("count") || n.Contains("score")) t.color = Color.white;
            else                          t.color = Color.white;
            // label must render ABOVE its button/chip background → make it the last sibling
            if (n.Contains("tap") || n.Contains("count") || n.Contains("score"))
                t.transform.SetAsLastSibling();
            EditorUtility.SetDirty(t);
        }
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Text>(true))
        { t.fontStyle = FontStyle.Bold; t.color = Color.white; EditorUtility.SetDirty(t); }

        // the score chip's backing image has a generic name — reach it via the UIManager field
        var uim = UnityEngine.Object.FindObjectOfType<_Scripts.Core.UIManager>(true);
        if (uim != null && uim.playerCountText != null)
        {
            var back = uim.playerCountText.transform.parent?.GetComponent<Image>();
            if (back != null) { back.color = chipDark; EditorUtility.SetDirty(back); }
        }

        Debug.Log("[ThemeSetup] UI styled (panels translucent, buttons blue, text white)");
    }
}
