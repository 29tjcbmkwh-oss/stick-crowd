// Visual Reskin Spec §5: import Russo One (OFL-licensed, from Google Fonts / the
// google/fonts repo — see Assets/_Assets/Fonts/RussoOne-OFL-LICENSE.txt) and generate a
// TMP Font Asset from it via TMP_FontAsset.CreateFontAsset, then set it as the project-wide
// TMP default so every existing TextMeshPro/TextMeshProUGUI object (HUD, gate labels, splash,
// popups) picks it up without hand-editing each one. Marker: font-setup-request.
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FontSetup
{
    private const string TtfPath  = "Assets/_Assets/Fonts/RussoOne-Regular.ttf";
    private const string AssetOut = "Assets/_Assets/Fonts/RussoOne SDF.asset";

    [MenuItem("Tools/Stickman/Apply Russo One Font")]
    public static void Run()
    {
        try
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (sourceFont == null)
                throw new FileNotFoundException($"Russo One .ttf not found/imported at {TtfPath}");

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetOut);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                if (fontAsset == null)
                    throw new System.Exception("TMP_FontAsset.CreateFontAsset returned null");
                AssetDatabase.CreateAsset(fontAsset, AssetOut);
                if (fontAsset.material != null)
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                if (fontAsset.atlasTextures != null)
                    foreach (var tex in fontAsset.atlasTextures)
                        if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }

            // TMP_Settings.defaultFontAsset is get-only in this TMP version (3.0.6) — the
            // backing field m_defaultFontAsset is private, so this goes through
            // SerializedObject rather than assuming a public setter exists (verified against
            // the package source before writing this, not guessed).
            var settingsInstance = TMP_Settings.instance;
            if (settingsInstance == null)
                throw new System.Exception("TMP_Settings.instance is null — TMP Essential Resources may not be imported");
            var so = new SerializedObject(settingsInstance);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
                throw new System.Exception("TMP_Settings has no m_defaultFontAsset serialized field (TMP version mismatch?)");
            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settingsInstance);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FontSetup] SUCCESS — Russo One TMP Font Asset created/verified at {AssetOut} " +
                      "and set as TMP_Settings.defaultFontAsset.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FontSetup] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }
}
