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
    // Danger gates use #FF4D2F (spec §1 lists it as the danger alternative), NOT brand orange:
    // orange is OUR enemy-crowd color, so orange-as-danger-gate collides with our own IP
    // (HOD A-list item A8, 2026-07-21). Red-family danger also matches the genre reference.
    private static readonly Color GateOrange  = new Color(1f, 0.302f, 0.184f, 0.82f);
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
        Color glow = new Color(c.r, c.g, c.b) * GateEmission;
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

    // Gate emission multiplier — single source shared with VisualOverhaul. 1.35x and 0.55x
    // both bleached the brand hues toward white over the bright environment (2026-07-20; see
    // the Transparent() comment below), so the "make the gates glow" retune (HOD 2026-07-23
    // item 2) deliberately does NOT crank this again: the visible glow comes from the bloom
    // pass instead (threshold lowered below gate luminance in PostProcessingSetup), which
    // extracts a blue/orange-dominant halo and preserves hue. 0.32x is a nudge to keep the
    // gates among the brightest surfaces so the bloom favours them — still under the 0.55x
    // known-bleach line.
    public const float GateEmission = 0.32f;

    public static string GateLabelText(Corridor c) => c.GetCorridorType() switch
    {
        Constants.CorridorTypes.Increase => $"+{c.increaseAmount}",
        Constants.CorridorTypes.Decrease => $"-{c.decreaseAmount}",
        Constants.CorridorTypes.Multiply => $"x{c.multiplyAmount}",
        Constants.CorridorTypes.Divide   => $"÷{c.divideAmount}",
        _ => "?"
    };

    private static void EnsureGateLabel(Corridor c)
    {
        const string labelName = "GateLabel";

        // Scene corridors are prefab instances — children inherited from the prefab cannot be
        // destroyed there. Update an existing label in place; only create when absent.
        var existing = c.transform.Find(labelName);
        TextMeshPro tmp;
        if (existing != null)
        {
            tmp = existing.GetComponent<TextMeshPro>();
            if (tmp == null) return;
        }
        else
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(c.transform, false);
            tmp = go.AddComponent<TextMeshPro>();
        }

        tmp.text = GateLabelText(c);
        StyleGateLabel(tmp, c);
        EditorUtility.SetDirty(tmp);
    }

    // The one authoritative gate-label style, applied to BOTH existing and newly-created
    // labels (the old code only restyled new ones — existing scene labels kept whatever an
    // earlier pass left, which is how the oversized overlapping white smear survived a "fixed"
    // report), and called by VisualOverhaul too so the two passes can no longer fight.
    //
    // Flush-on-panel positioning (HOD 2026-07-21): sits against the panel's front face with a
    // 0.03 z-offset only (z-fighting), rotating WITH the gate like paint on glass — which is
    // why any Billboard component on the label must be removed, not kept: Billboard re-rotates
    // the label off the panel every frame and was one of the two runtime underminers of the
    // flush fix (the other was GatePulse resetting scale to 1 — fixed in GatePulse itself).
    public static void StyleGateLabel(TextMeshPro tmp, Corridor c)
    {
        var go = tmp.gameObject;

        var billboard = go.GetComponent<_Scripts.Core.Billboard>();
        if (billboard != null) UnityEngine.Object.DestroyImmediate(billboard, true);
        if (go.GetComponent<GatePulse>() == null) go.AddComponent<GatePulse>();

        go.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        // Identity rotation, NOT the old Euler(0,180,0): that flip dated from the Billboard
        // era when runtime facing was overridden every frame anyway. Once Billboard was
        // removed the static 180° left the text facing away from the camera, rendering
        // mirrored via TMP's double-sided shader (confirmed in the 16:45 capture — "+20"
        // read as "0S+").
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.16f;

        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        // Near-black fill + thick white outline (was SurfaceDark navy + 0.18 outline, which
        // read tone-on-tone against the colored glass at distance — "readable close, muddy
        // far" in the 18:07 captures; Ali flagged labels as still not fixed).
        tmp.color = new Color(0.03f, 0.04f, 0.08f, 1f);
        if (tmp.fontSharedMaterial != null)
        {
            tmp.outlineWidth = 0.28f;
            tmp.outlineColor = Color.white;
        }

        // Clamp the rect to the gate panel's own footprint and autosize the text inside it —
        // a fixed fontSize with wrapping off overflows the rect freely, which is how two
        // neighbouring labels smeared across each other's gates.
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var renderer = c.GetComponent<MeshRenderer>();
        float panelW = renderer != null ? renderer.bounds.size.x : 3f;
        float panelH = renderer != null ? Mathf.Max(0.6f, renderer.bounds.size.y) : 1.2f;
        float lossyX = Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.x));
        float lossyY = Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.y));
        tmp.rectTransform.sizeDelta = new Vector2(panelW * 0.88f / lossyX,
                                                  panelH * 0.70f / lossyY);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 60f;
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
            // The world-space counter bubble + tail are brand blue by spec (A4: white number
            // on brand-blue speech bubble). "counterbubble" contains "count", so without this
            // exemption the chipDark branch below repaints it dark every theme pass.
            if (n == "counterbubble" || (n == "tail" && img.transform.parent != null &&
                img.transform.parent.name == "CounterBubble"))
            { img.color = BrandPalette.Blue; EditorUtility.SetDirty(img); continue; }
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
            // The crowd counter sits on the brand-blue bubble chip (VisualOverhaul
            // AddCounterBubble) — white-on-blue. The old brand-blue-text rule predates the
            // chip and made the number invisible against its own background.
            if (t == crowdCounter)       { t.color = Color.white; }
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

        StyleWinPanel(uim);

        Debug.Log("[ThemeSetup] UI styled (panels translucent, buttons blue, text white)");
    }

    // Reference win screen (HOD dispatch, Count Master frames): dark navy backdrop,
    // "EARNED +N" (text set by ScoreManager at runtime), coin fountain (UIManager, runtime),
    // and ONE large green rounded NEXT LEVEL button. This pass restyles the existing panel's
    // serialized scene state — same runtime-reference/no-YAML-hand-editing pattern as the
    // rest of StyleUI.
    private static void StyleWinPanel(_Scripts.Core.UIManager uim)
    {
        if (uim == null || uim.gameWinPanel == null) return;
        var panel = uim.gameWinPanel;

        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Assets/Stickman/Textures/Tex_RoundedRect.png");

        // coin sprite for the runtime fountain
        if (rounded != null && uim.coinSprite != rounded)
        { uim.coinSprite = rounded; EditorUtility.SetDirty(uim); }

        // Full-screen opaque dark navy backdrop (reference win screens are a full dark
        // takeover, not a floating card) — also hides the world behind it: the tilted 3D
        // scoreboard prop, bonus strip, and resting ball all read as clutter/skew through a
        // partial panel (Ali flagged the "skewed card" in the 19:07/20:39 captures; the skew
        // was world-geometry perspective bleeding through, not a UI rotation).
        var bg = panel.GetComponent<Image>();
        if (bg == null) bg = panel.AddComponent<Image>(); // root had no Image — the "backdrop" was world geometry bleeding through
        {
            bg.color = new Color(BrandPalette.SurfaceDark.r, BrandPalette.SurfaceDark.g,
                                 BrandPalette.SurfaceDark.b, 1f);
            bg.sprite = null; // plain full-bleed quad, no rounded corners on a full-screen fill
            // Plain full-stretch, NOT overscan: the canvas is screen-space, so stretch covers
            // the screen exactly — the earlier ±4000 overscan dragged the panel's bottom-
            // anchored children (the NEXT LEVEL button) 4000px off-screen (20:46 capture).
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(bg);
        }

        // Straighten the authored tilt: the win card children carry decorative z-rotations
        // that read as "skewed, not a flat clean panel" against the reference (Ali/HOD,
        // 19:07 capture). Reference win cards are axis-aligned.
        foreach (var rt2 in panel.GetComponentsInChildren<RectTransform>(true))
        {
            if (Mathf.Abs(rt2.localEulerAngles.z) > 0.5f && Mathf.Abs(rt2.localEulerAngles.z - 360f) > 0.5f
                && rt2.gameObject.name != "Tail")
            { rt2.localRotation = Quaternion.identity; EditorUtility.SetDirty(rt2); }
        }

        var green = new Color(0.24f, 0.83f, 0.39f, 1f);
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            // Only the actual next-level button gets the big green treatment + label —
            // the panel may also hold e.g. the double-reward ad button, which must keep
            // its own identity.
            bool isNextLevel = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == "NextLevelButton") isNextLevel = true;
            if (!isNextLevel) continue;

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = green;
                if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
                EditorUtility.SetDirty(img);
            }
            var rt = (RectTransform)button.transform;
            if (rt.sizeDelta.x > 0 && rt.sizeDelta.x < 420f)
            { rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 380f), Mathf.Max(rt.sizeDelta.y, 110f)); EditorUtility.SetDirty(rt); }
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            { label.text = "NEXT LEVEL"; label.color = Color.white; label.fontStyle = FontStyles.Bold; EditorUtility.SetDirty(label); }
            var legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            { legacyLabel.text = "NEXT LEVEL"; legacyLabel.color = Color.white; legacyLabel.fontStyle = FontStyle.Bold; EditorUtility.SetDirty(legacyLabel); }
        }

        foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
        { t.color = Color.white; EditorUtility.SetDirty(t); }

        // The earned-score text rect was authored for a 3-4 digit number; "EARNED +210"
        // wrapped one syllable per line in the 19:02 win capture. Wide rect, no wrap,
        // autosized.
        var score = UnityEngine.Object.FindObjectOfType<ScoreManager>(true);
        if (score != null && score.endGameScoreText != null)
        {
            var st = score.endGameScoreText;
            st.enableWordWrapping = false;
            st.enableAutoSizing = true;
            st.fontSizeMin = 24f;
            st.fontSizeMax = 96f;
            st.alignment = TextAlignmentOptions.Center;
            var srt = st.rectTransform;
            srt.sizeDelta = new Vector2(760f, 150f);
            EditorUtility.SetDirty(st);
        }
    }
}
