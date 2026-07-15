// Headless setup: swaps the capsule "Cat" crowd unit and the LevelEndBoss visual for the
// MIT stickman character (from CountMasters_Prototype), builds a matching AnimatorController
// (startRunning / stopRunning triggers, as Cat.cs expects), and applies the Blue-vs-Orange theme.
//
// Run from CLI:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod StickmanSetup.Run
// or in-editor via  Tools > Stickman > Run Full Setup.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class StickmanSetup
{
    private const string ModelPath      = "Assets/_Assets/Stickman/Models/Player.fbx";
    private const string IdleFbxPath    = "Assets/_Assets/Stickman/Animations/Player@Idle.fbx";
    private const string RunFbxPath     = "Assets/_Assets/Stickman/Animations/Player@Run.fbx";
    private const string ControllerPath = "Assets/_Assets/Stickman/Animations/StickmanCrowd.controller";
    private const string BlueMatPath    = "Assets/_Assets/Stickman/Materials/Mat_StickBlue.mat";
    private const string OrangeMatPath  = "Assets/_Assets/Stickman/Materials/Mat_StickOrange.mat";
    private const string CatPrefabPath  = "Assets/_Assets/PLAYER/ExampleCat.prefab";
    private const string BossPrefabPath = "Assets/_Assets/ENEMY/LevelEndBoss.prefab";

    [MenuItem("Tools/Stickman/Run Full Setup")]
    public static void Run()
    {
        try
        {
            EnsureLooping(IdleFbxPath);
            EnsureLooping(RunFbxPath);
            var controller = BuildController();
            var blue   = BuildMaterial(BlueMatPath,   new Color(0.20f, 0.45f, 1f));
            var orange = BuildMaterial(OrangeMatPath, new Color(1f, 0.55f, 0.10f));
            SwapCrowdUnit(controller, blue);
            SwapBossVisual(controller, orange);
            AssetDatabase.SaveAssets();
            Debug.Log("[StickmanSetup] SUCCESS — crowd + boss now use the stickman rig.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StickmanSetup] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    // ---- 1. make sure the idle/run clips loop --------------------------------------------
    private static void EnsureLooping(string fbxPath)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        if (importer == null) throw new Exception($"No ModelImporter at {fbxPath}");
        // clipAnimations is empty until customized; start from defaults then force looping.
        var clips = importer.clipAnimations is { Length: > 0 }
            ? importer.clipAnimations
            : importer.defaultClipAnimations;
        foreach (var c in clips) { c.loopTime = true; c.loopPose = true; }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log($"[StickmanSetup] looping enabled on {fbxPath} ({clips.Length} clips)");
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null) throw new Exception($"No AnimationClip found inside {fbxPath}");
        return clip;
    }

    // ---- 2. controller with the exact triggers Cat.cs fires ------------------------------
    private static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath); // idempotent re-runs
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("startRunning", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("stopRunning",  AnimatorControllerParameterType.Trigger);

        var sm   = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = LoadClip(IdleFbxPath);
        var run  = sm.AddState("Run");  run.motion  = LoadClip(RunFbxPath);
        sm.defaultState = idle;

        var toRun = idle.AddTransition(run);
        toRun.hasExitTime = false; toRun.duration = 0.1f;
        toRun.AddCondition(AnimatorConditionMode.If, 0, "startRunning");

        var toIdle = run.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.If, 0, "stopRunning");

        Debug.Log("[StickmanSetup] controller built");
        return ctrl;
    }

    // ---- 3. themed materials --------------------------------------------------------------
    private static Material BuildMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f); // flat HC look
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject AttachRig(GameObject parent, AnimatorController controller,
                                        Material mat, float footY, float scale)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new Exception($"Model not found at {ModelPath}");

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(model);
        rig.name = "StickmanRig";
        rig.transform.SetParent(parent.transform, false);      // appended LAST: keeps GetChild(n) indices
        rig.transform.localPosition = new Vector3(0f, footY, 0f);
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one * scale;

        var animator = rig.GetComponent<Animator>();
        if (animator == null) animator = rig.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;                       // movement is code-driven

        foreach (var r in rig.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        return rig;
    }

    // ---- 4. the crowd unit ------------------------------------------------------------------
    private static void SwapCrowdUnit(AnimatorController controller, Material blue)
    {
        var root = PrefabUtility.LoadPrefabContents(CatPrefabPath);
        try
        {
            if (root.transform.Find("StickmanRig") != null)
            { Debug.Log("[StickmanSetup] crowd already swapped, skipping"); return; }

            // feet position from the capsule collider so the rig stands on the same ground plane
            var col = root.GetComponent<CapsuleCollider>();
            var footY = col != null ? col.center.y - col.height * 0.5f : 0f;

            // strip the capsule visual + the root Animator (Cat.cs must find the rig's animator;
            // GetComponentInChildren checks the root FIRST, so the root one has to go)
            StripComponent<MeshFilter>(root);
            StripComponent<MeshRenderer>(root);
            StripComponent<Animator>(root);

            AttachRig(root, controller, blue, footY, 1f);
            PrefabUtility.SaveAsPrefabAsset(root, CatPrefabPath);   // same path => GUID preserved
            Debug.Log($"[StickmanSetup] crowd unit swapped (footY={footY:F2})");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---- 5. the boss --------------------------------------------------------------------------
    private static void SwapBossVisual(AnimatorController controller, Material orange)
    {
        var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            // find whichever GameObject renders the built-in capsule
            var capsuleMf = root.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(mf => mf.sharedMesh != null && mf.sharedMesh.name == "Capsule");
            if (capsuleMf == null)
            { Debug.LogWarning("[StickmanSetup] boss: no capsule visual found, skipping"); return; }

            var host = capsuleMf.gameObject;
            if (host.transform.Find("StickmanRig") != null)
            { Debug.Log("[StickmanSetup] boss already swapped, skipping"); return; }

            var col = host.GetComponent<CapsuleCollider>();
            var footY = col != null ? col.center.y - col.height * 0.5f : -1f;

            StripComponent<MeshRenderer>(host);   // visual only — collider, children, scripts untouched
            StripComponent<MeshFilter>(host);
            AttachRig(host, controller, orange, footY, 2.2f);   // bigger = menacing

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Debug.Log("[StickmanSetup] boss visual swapped");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StripComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) UnityEngine.Object.DestroyImmediate(c);
    }
}
