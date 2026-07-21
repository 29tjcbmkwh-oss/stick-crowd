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
    // ---- palette: Visual Reskin Spec 2026-07-20 (bright/toy-like, replaces the old dark
    // track + flat-green void — that dark-track look was a large part of why the build read
    // as prototype-grade; see the spec's "single biggest art-direction call") ----
    private static readonly Color SkyFallback = BrandPalette.SkyHorizon;   // flat-color fallback if skybox isn't applied
    private static readonly Color TrackLight  = BrandPalette.GroundLight; // #EDEFF4
    private static readonly Color WallLight   = BrandPalette.LaneEdge;    // #C9D2E6, subtle not dark
    private static readonly Color ObstacleRed = new Color(0.95f, 0.23f, 0.25f);  // static hazard prop, not a gate choice
    private static readonly Color GateBlue    = BrandPalette.BlueTranslucent;
    private static readonly Color GateOrange  = BrandPalette.OrangeTranslucent;
    private const string MatDir = "Assets/_Assets/Stickman/Materials";

    [MenuItem("Tools/Stickman/Apply Theme")]
    public static void Run()
    {
        try
        {
            var trackMat = Opaque("Mat_TrackLight", TrackLight);
            var wallMat  = Opaque("Mat_WallLight", WallLight);
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
        // Flat matte toy look per Visual Reskin Spec: Smoothness ~0.10-0.20, Metallic 0.
        // (0.05 kept as a slightly flatter floor than crowd materials for the environment.)
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
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

        // GATE GLOW — CORRECTED 2026-07-20 (HOD) after the first real gameplay capture.
        // The original 1.35x emission was tuned for the DARK-track reference and was actively
        // harmful over the bright environment: emission pushes a colour toward white, 45% alpha
        // over a white background pushes it toward white again, and the two stacked until the
        // gates rendered as pale cyan / pale lemon instead of blue / orange. Hue identity beats
        // glow — the game is literally named after these two colours.
        // Now: emission is a subtle lift (0.22x) that survives bloom without bleaching the hue,
        // and saturation comes from the raised alpha in BrandPalette instead.
        Color glow = new Color(c.r, c.g, c.b) * 0.22f;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetFloat("_Glossiness", 0.7f); // glassy sheen, not matte
        m.SetFloat("_Metallic", 0f);

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
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        // FIXED 2026-07-21 (HOD): this label used to float 0.6 units ABOVE the gate panel as a
        // separate hovering sign, which read as "on top of" the gate, not part of it (owner
        // feedback, directly comparing against reference footage where the number is printed ON
        // the glass). Now sits nearly flush against the panel's own front face — a tiny forward
        // offset (0.03) only to avoid z-fighting with the gate mesh, no vertical offset at all —
        // so it reads as printed on the surface instead of a sign hovering over it.
        go.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = Vector3.one * 0.16f; // slightly larger since it's now on-surface, not elevated
        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20, 8);
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

        // Flat-color fallback; VisualOverhaul.cs applies the actual gradient skybox afterward
        // and switches clearFlags to Skybox — this just avoids a jarring dark camera background
        // if ThemeSetup is ever run standalone without the visual-overhaul pass following it.
        foreach (var cam in UnityEngine.Object.FindObjectsOfType<Camera>(true))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyFallback;
        }

        // proper starting crowd
        foreach (var f in UnityEngine.Object.FindObjectsOfType<FormationBase>(true))
            if (f.Amount < 3) f.Amount = 3;

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

        var crowdCounter = UnityEngine.Object.FindObjectOfType<_Scripts.Core.UIManager>(true)?.playerCountText;
        foreach (var t in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true))
        {
            var n = t.gameObject.name.ToLowerInvariant();
            t.fontStyle = FontStyles.Bold;
            // The crowd counter is a world-space chip over the bright sky — white is invisible
            // there (UIManager.StyleCrowdCounter owns its look: brand blue + white outline).
            if (t == crowdCounter)       { t.color = _Scripts.Utilities.BrandPalette.Blue; }
            else if (n.Contains("tap"))  { t.text = "TAP TO PLAY"; t.color = Color.white; }
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
