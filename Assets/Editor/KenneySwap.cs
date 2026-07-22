// Swaps the crowd unit's rig to Kenney's "Animated Characters Retro" characterMedium (CC0 —
// see Assets/_Assets/Kenney/Kenney-CC0-LICENSE.txt), the pack earmarked as skin content
// (HOD Group B / 2026-07-22 dispatch item 1). The Kenney skin textures are UV-mapped for
// THIS model, which is why the texture skins require the model swap — they render as UV
// garbage on the previous Sketchfab rig. Boss keeps the Sketchfab character (orange, 2.2x):
// visual distinction, less churn. Mirrors CharacterSwap.cs's proven humanoid-retarget flow.
// Marker: kenney-swap-request.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using _Scripts.Utilities;

public static class KenneySwap
{
    private const string CharFbx   = "Assets/_Assets/Kenney/characterMedium.fbx";
    // The project's proven Mixamo humanoid clips, NOT the pack's own animation FBXes:
    // Kenney's run/idle fbx rigs failed humanoid retargeting (empty clips -> the whole
    // crowd T-posed in the 11:42 captures). Humanoid Mixamo clips retarget onto any
    // humanoid avatar, including characterMedium.
    private const string RunFbx    = "Assets/_Assets/Stickman/Animations/Player@Run.fbx";
    private const string IdleFbx   = "Assets/_Assets/Stickman/Animations/Player@Idle.fbx";
    private const string Controller = "Assets/_Assets/Kenney/Kenney.controller";
    private const string MatDir    = "Assets/_Assets/Kenney";
    private const string CatPrefab = "Assets/_Assets/PLAYER/ExampleCat.prefab";

    [MenuItem("Tools/Stickman/Swap In Kenney Character")]
    public static void Run()
    {
        try
        {
            SetHumanoid(CharFbx);
            SetHumanoidLooping(RunFbx);
            SetHumanoidLooping(IdleFbx);

            var avatar = AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null) throw new Exception("No Humanoid avatar generated for the Kenney FBX");

            var controller = BuildController();
            // Default CLASSIC skin: brand blue tint, no texture — the game's whole identity
            // is the blue crowd; textured skins are the store unlocks (SkinSystem).
            var blue = Mat("Mat_KenneyBlue", BrandPalette.Blue);

            SwapCharacter(CatPrefab, controller, avatar, blue, 1.0f);

            AssetDatabase.SaveAssets();
            Debug.Log("[KenneySwap] SUCCESS — Kenney characterMedium swapped into the crowd unit.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[KenneySwap] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    private static void SetHumanoid(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) throw new Exception($"No ModelImporter at {path}");
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.SaveAndReimport();
    }

    private static void SetHumanoidLooping(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) throw new Exception($"No ModelImporter at {path}");
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        var clips = imp.clipAnimations is { Length: > 0 } ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips) { c.loopTime = true; c.loopPose = true; }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    private static AnimationClip HumanoidClip(string fbx)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null) throw new Exception($"No AnimationClip in {fbx}");
        return clip;
    }

    private static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(Controller);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controller);
        // Same trigger names Cat.ControlAnimationState drives — do not rename.
        ctrl.AddParameter("startRunning", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("stopRunning",  AnimatorControllerParameterType.Trigger);

        var sm   = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = HumanoidClip(IdleFbx);
        var run  = sm.AddState("Run");  run.motion  = HumanoidClip(RunFbx);
        sm.defaultState = idle;

        var toRun = idle.AddTransition(run);
        toRun.hasExitTime = false; toRun.duration = 0.12f;
        toRun.AddCondition(AnimatorConditionMode.If, 0, "startRunning");
        var toIdle = run.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.12f;
        toIdle.AddCondition(AnimatorConditionMode.If, 0, "stopRunning");
        return ctrl;
    }

    private static Material Mat(string name, Color c)
    {
        var path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Bounds RigBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) throw new Exception("Character has no renderers");
        var b = rends[0].bounds;
        for (var i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    private static void SwapCharacter(string prefabPath, AnimatorController controller,
                                      Avatar avatar, Material mat, float heightMultiplier)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var name in new[] { "StickmanRig", "CharacterRig" })
            {
                var old = FindChild(root.transform, name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var host = root;
            var col = host.GetComponentInChildren<CapsuleCollider>();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            rig.name = "CharacterRig";
            rig.transform.SetParent(host.transform, false);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            var animator = rig.GetComponent<Animator>() ?? rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            foreach (var r in rig.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

            var lossyY = col != null ? col.transform.lossyScale.y : host.transform.lossyScale.y;
            var targetH = (col != null ? col.height : 2f) * lossyY * heightMultiplier;
            var b = RigBounds(rig);
            if (b.size.y > 1e-4f) rig.transform.localScale *= targetH / b.size.y;
            b = RigBounds(rig);
            rig.transform.position += new Vector3(0f, host.transform.position.y - b.min.y, 0f);

            var cat = root.GetComponent<_Scripts.Models.Cat>();
            if (cat != null) cat.rotationTarget = rig.transform;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[KenneySwap] {System.IO.Path.GetFileName(prefabPath)}: Kenney rig in (h={targetH:F2})");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
