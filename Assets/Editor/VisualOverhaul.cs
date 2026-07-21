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
            RepairDefaultFont();
            ThemeGatesInPrefab("Assets/_Assets/ENV/Level1.prefab", plate);
            ThemeGatesInPrefab("Assets/_Assets/ENV/Corridor1.prefab", plate);
            AddCounterBubble();
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
            SquashGatePanel(corridor);
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

        // 3) dark posts framing each gate panel (HOD A-list A1): the reference gates read as
        // glass panels strung between two vertical posts; without posts ours read as floating
        // slabs. Simple cylinders at the panel's x-edges, idempotent by child name.
        foreach (var corridor in root.GetComponentsInChildren<Corridor>(true))
            AddGatePosts(corridor);

        return count;
    }

    // HOD directive 2026-07-21: reference gates are SHORT-AND-WIDE glass panels; ours were
    // full-height slabs. Squash the panel to ~0.65 world-units tall, keeping the BOTTOM edge
    // where it was so the trigger volume still overlaps the crowd's bodies (cat capsules span
    // ~0.8 units up from the track — a centered squash would lift the gate off the ground and
    // open a pass-under gap). Idempotent: skips panels already at target height.
    private static void SquashGatePanel(Corridor c)
    {
        var r = c.GetComponent<MeshRenderer>();
        if (r == null) return;
        const float targetHeight = 0.65f;
        var b = r.bounds;
        if (b.size.y < 0.01f || Mathf.Abs(b.size.y - targetHeight) < 0.05f) return;
        float f = targetHeight / b.size.y;
        var t = c.transform;
        var ls = t.localScale;
        t.localScale = new Vector3(ls.x, ls.y * f, ls.z);
        // scaling happens about the pivot; shift so the old bottom edge is preserved
        float pivotY = t.position.y;
        float newBottom = pivotY - (pivotY - b.min.y) * f;
        t.position += Vector3.up * (b.min.y - newBottom);
    }

    private static void AddGatePosts(Corridor c)
    {
        var r = c.GetComponent<MeshRenderer>();
        if (r == null) return;
        var postMat = PostMaterial();
        var b = r.bounds; // world-space; convert to corridor-local via InverseTransformPoint
        foreach (var side in new[] { -1f, 1f })
        {
            string name = side < 0 ? "GatePost_L" : "GatePost_R";
            // Update-in-place, never destroy+recreate: in Level1.prefab the corridors are
            // NESTED Corridor1 instances, and destroying a child inherited from the nested
            // prefab throws InvalidOperationException and aborts the whole pass (hit
            // 2026-07-21 18:06). Post size derives from panel bounds, which the
            // short-and-wide squash changes, so existing posts must be re-fit, not skipped.
            var existing = c.transform.Find(name);
            GameObject post;
            if (existing == null)
            {
                post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = name;
                Object.DestroyImmediate(post.GetComponent<Collider>()); // decorative only
                post.transform.SetParent(c.transform, false);
            }
            else post = existing.gameObject;

            var worldEdge = new Vector3(b.center.x + side * b.extents.x, b.center.y, b.center.z);
            var local = c.transform.InverseTransformPoint(worldEdge);
            float postWorldHeight = b.size.y * 1.35f;
            float lossyY = Mathf.Max(0.0001f, Mathf.Abs(c.transform.lossyScale.y));
            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(c.transform.lossyScale.x));
            post.transform.localPosition = new Vector3(local.x, local.y + (postWorldHeight - b.size.y) * 0.5f / lossyY, local.z);
            // cylinder primitive is 2 units tall at scale 1
            post.transform.localScale = new Vector3(0.22f / lossyX, postWorldHeight * 0.5f / lossyY, 0.22f / lossyX);
            post.GetComponent<MeshRenderer>().sharedMaterial = postMat;
            EditorUtility.SetDirty(post);
        }
    }

    private static Material PostMaterial()
    {
        var path = $"{MatDir}/Mat_GatePost.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(0.10f, 0.13f, 0.22f, 1f)); // dark navy, matches label fill
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // HOD A-list A4: the reference crowd counter is a white number on a rounded brand-blue
    // chip with a tail, not bare floating text. Adds a rounded-rect Image + diamond tail
    // behind the existing PlayerCount TMP inside Example Army.prefab's world-space canvas.
    private static void AddCounterBubble()
    {
        const string prefabPath = "Assets/_Assets/PLAYER/Example Army.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            var count = canvas.transform.Find("PlayerCount");
            if (count == null) return;

            var sprite = RoundedRectSprite();
            var bubbleT = canvas.transform.Find("CounterBubble");
            if (bubbleT == null)
            {
                var bubble = new GameObject("CounterBubble");
                bubble.transform.SetParent(canvas.transform, false);
                var img = bubble.AddComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;

                var tail = new GameObject("Tail");
                tail.transform.SetParent(bubble.transform, false);
                var tailImg = tail.AddComponent<UnityEngine.UI.Image>();
                var trt = tailImg.rectTransform;
                trt.sizeDelta = new Vector2(0.22f, 0.22f);
                trt.anchoredPosition = new Vector2(0f, -0.31f);
                trt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                bubbleT = bubble.transform;
            }

            // Re-asserted every pass (create AND update): a world-space canvas depth-sorts
            // separate material batches, so the bubble must sit behind the number's authored
            // z (-0.08 = toward viewer) or the opaque chip occludes the text entirely —
            // hierarchy sibling order alone does NOT win here (confirmed in the 17:02/17:07
            // captures: bubble rendered, number gone).
            bubbleT.SetSiblingIndex(0);
            var bubbleRt = (RectTransform)bubbleT;
            bubbleRt.anchoredPosition3D = new Vector3(
                ((RectTransform)count).anchoredPosition.x,
                ((RectTransform)count).anchoredPosition.y, 0.02f);
            bubbleRt.sizeDelta = new Vector2(1.05f, 0.62f);
            var bubbleImg = bubbleT.GetComponent<UnityEngine.UI.Image>();
            if (bubbleImg != null) bubbleImg.color = BrandPalette.Blue;
            var tailT = bubbleT.Find("Tail");
            if (tailT != null)
            {
                var ti2 = tailT.GetComponent<UnityEngine.UI.Image>();
                if (ti2 != null) ti2.color = BrandPalette.Blue;
            }

            // white number on the blue chip (was brand-blue-on-sky when it floated bare)
            var tmp = count.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                // Pin the font explicitly: the label's original font guid (8a89fa14...) is
                // dangling — the asset no longer exists — so on any prefab resave TMP swaps
                // to the TMP-default "RussoOne SDF", whose asset is BROKEN (atlas fileID 0,
                // atlasWidth 0 — created via the scripted CreateFontAsset path the Reskin
                // Spec §5 warned against) and renders zero glyphs. This was the invisible-
                // counter bug in the 17:02-17:12 captures. Pin to the package's known-good
                // LiberationSans SDF.
                var goodFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath("8f586378b4e144a9851e7b34d9b748ee"));
                if (goodFont != null) tmp.font = goodFont;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                count.SetAsLastSibling();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[VisualOverhaul] counter bubble chip added to Example Army canvas");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // The scripted "RussoOne SDF" TMP asset is broken (atlas fileID 0, atlasWidth 0 — the
    // CreateFontAsset shortcut the Reskin Spec §5 warned against); every TMP that falls back
    // to the project default renders zero glyphs. Until Russo One is rebuilt through the TMP
    // Font Asset Creator GUI (needs a human), the project default must be the known-good
    // LiberationSans SDF so default-font consumers (crowd counter, leaderboard button, any
    // future TMP) stay visible. Same SerializedObject route FontSetup uses.
    private static void RepairDefaultFont()
    {
        var russo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Assets/Fonts/RussoOne SDF.asset");
        bool russoBroken = russo == null || russo.atlasTexture == null || russo.atlasTexture.width == 0;
        if (!russoBroken) return;

        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            AssetDatabase.GUIDToAssetPath("8f586378b4e144a9851e7b34d9b748ee"));
        if (liberation == null || TMP_Settings.instance == null) return;
        if (TMP_Settings.defaultFontAsset == liberation) return;

        var so = new SerializedObject(TMP_Settings.instance);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null) return;
        prop.objectReferenceValue = liberation;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(TMP_Settings.instance);
        Debug.Log("[VisualOverhaul] TMP default font -> LiberationSans SDF (RussoOne SDF asset is broken; needs GUI rebuild)");
    }

    private const string TexDir = "Assets/_Assets/Stickman/Textures";

    private static Sprite RoundedRectSprite()
    {
        Directory.CreateDirectory(TexDir);
        var path = $"{TexDir}/Tex_RoundedRect.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        const int size = 64, radius = 18;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius)));
            float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(radius - d + 0.5f); // 1px AA edge
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteBorder = new Vector4(20, 20, 20, 20); // 9-slice so corners stay round at any size
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void OverhaulScene(Material plate)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);

        // Visual Reskin Spec 2026-07-20: pale-blue-to-white gradient skybox (#DFF1FF ->
        // #FFFFFF), replacing the earlier vivid-blue procedural sky. Built-in procedural
        // skybox shader (guaranteed to build on mobile) tuned toward a soft, airy, almost-flat
        // gradient rather than a saturated "vivid sky" — the top viral crowd-runners read as
        // bright and toy-like, not scenic.
        // Custom two-color gradient shader (HOD directive 2026-07-21): Skybox/Procedural
        // filters any tint through atmosphere scattering — at 0.35 thickness the spec blue
        // washed to white, at 1.0 it rendered teal. The gradient shader draws #1E7FE0 ->
        // #7FC4FF exactly as authored, no scattering model in the way.
        var gradient = Shader.Find("Skybox/BvORGradient");
        var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Sky.mat");
        if (sky == null || sky.shader != gradient)
        {
            AssetDatabase.DeleteAsset($"{MatDir}/Mat_Sky.mat");
            sky = new Material(gradient);
            AssetDatabase.CreateAsset(sky, $"{MatDir}/Mat_Sky.mat");
        }
        sky.SetColor("_TopColor", BrandPalette.SkyTop);
        sky.SetColor("_HorizonColor", BrandPalette.SkyHorizon);
        sky.SetFloat("_Exponent", 1.4f);
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

        AddTrackChevrons();   // A2: forward chevrons kill the blank-page track
        AddFinishLine();      // A5: checkered strip before the boss arena
        AddOuterWater();      // A3: saturated plane outside the track instead of white void

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[VisualOverhaul] scene: sky + fog + lighting + track dressing");
    }

    private static Material FlatMat(string name, Color c)
    {
        var path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject FlatQuad(string name, Transform parent, Material mat,
                                       Vector3 pos, Vector3 scale, float yaw)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.position = pos;
        // Quad faces +Z by default; rotate to lie flat on the ground, then yaw
        q.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        q.transform.localScale = scale;
        q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return q;
    }

    // The track's TOP surface sits at world y ≈ 2.4 (Ground local y 0.29 under the Level1
    // root at y 2.047, plus mesh thickness) — the first placement of these decals used
    // y ≈ 0 and buried all of them inside/under the track (invisible in the 17:36 capture).
    private const float TrackSurfaceY = 2.62f;
    private const float TrackCenterX = 0.5f; // walls at x -3.3 / +4.2

    private static Transform RebuildRoot(string rootName)
    {
        var old = GameObject.Find(rootName);
        if (old != null) Object.DestroyImmediate(old);
        return new GameObject(rootName).transform;
    }

    // A2: subtle forward-pointing chevrons down the track. Two thin flat quads per chevron
    // forming a V with the apex pointing +Z (run direction). Light neutral per spec §1 —
    // visible against #EDEFF4 but never competing with gates/crowd.
    private static void AddTrackChevrons()
    {
        var parent = RebuildRoot("TrackChevrons");
        // #A8B4D6 (HOD directive): the first #C7CFE3 was near-zero contrast on the #EDEFF4
        // track — if the viewer can't consciously see the chevrons they aren't doing their
        // speed-read job.
        var chevMat = FlatMat("Mat_Chevron", new Color(0.659f, 0.706f, 0.839f, 1f));
        for (float z = 12f; z <= 92f; z += 8f)
        {
            // arms meet at the apex (TrackCenterX, z+0.5); each arm sweeps back and outward
            FlatQuad("ChevL", parent, chevMat, new Vector3(TrackCenterX - 0.55f, TrackSurfaceY, z), new Vector3(1.5f, 0.28f, 1f), -55f);
            FlatQuad("ChevR", parent, chevMat, new Vector3(TrackCenterX + 0.55f, TrackSurfaceY, z), new Vector3(1.5f, 0.28f, 1f), 55f);
        }
    }

    // A5: black-and-white checkered strip across the track just before the boss arena.
    private static void AddFinishLine()
    {
        var parent = RebuildRoot("FinishLine");
        var boss = Object.FindObjectOfType<Boss>(true);
        float z = boss != null ? boss.transform.position.z - 4f : 100f;
        var dark = FlatMat("Mat_CheckerDark", new Color(0.12f, 0.12f, 0.14f, 1f));
        var light = FlatMat("Mat_CheckerLight", Color.white);
        const int cols = 10; const float trackW = 7f, cell = trackW / cols, depth = 0.7f;
        for (int row = 0; row < 2; row++)
        for (int i = 0; i < cols; i++)
        {
            var mat = ((i + row) % 2 == 0) ? dark : light;
            FlatQuad("Check", parent, mat,
                new Vector3(TrackCenterX - trackW * 0.5f + cell * (i + 0.5f), TrackSurfaceY + 0.01f, z + row * depth),
                new Vector3(cell, depth, 1f), 0f);
        }
    }

    // A3: saturated water-blue plane outside the track so the offtrack void has color.
    // Sits below the track's top surface so it never z-fights the track or walls.
    private static void AddOuterWater()
    {
        var parent = RebuildRoot("OuterWater");
        var waterMat = FlatMat("Mat_OuterWater", new Color(0.24f, 0.62f, 0.90f, 1f)); // #3D9EE6
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "WaterPlane";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(TrackCenterX, 1.6f, 60f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(400f, 400f, 1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
    }
}
