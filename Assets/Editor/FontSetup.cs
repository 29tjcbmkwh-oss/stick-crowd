// Visual Reskin Spec §5: import Russo One (OFL-licensed, from Google Fonts / the
// google/fonts repo — see Assets/_Assets/Fonts/RussoOne-OFL-LICENSE.txt) and generate a
// TMP Font Asset from it via TMP_FontAsset.CreateFontAsset, then set it as the project-wide
// TMP default so every existing TextMeshPro/TextMeshProUGUI object (HUD, gate labels, splash,
// popups) picks it up without hand-editing each one. Marker: font-setup-request.
//
// ROOT CAUSE FOUND 2026-07-23 (HOD font investigation): the scripted CreateFontAsset path is
// NOT fundamentally broken in this Editor — it creates a DYNAMIC-population asset whose atlas
// texture starts 0x0 with zero glyphs (see TMP_FontAsset.cs:492 — `new Texture2D(0, 0, ...)`).
// The "dead atlas / renders zero glyphs" state is simply that empty birth state serialized to
// disk. Dynamic mode self-populates when text using the font renders in-editor, and one of the
// 2026-07-22 SaveAssets passes persisted exactly that: the on-disk asset healed to a 1024x1024
// atlas with 51 baked glyphs (22.4% non-zero pixels, measured from the serialized YAML).
// The fix is to force that bake at creation time: TryAddCharacters(all printable ASCII), then
// save — no Font Asset Creator GUI needed. Verified against the disk YAML after save, not the
// in-memory object.
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FontSetup
{
    private const string TtfPath  = "Assets/_Assets/Fonts/RussoOne-Regular.ttf";
    private const string AssetOut = "Assets/_Assets/Fonts/RussoOne SDF.asset";

    // Every printable ASCII character (0x20-0x7E): full coverage for HUD numbers, gate labels,
    // store text, and the splash wordmark. Missing glyphs would silently render as blanks.
    private const string BakeSet =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~";

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

            // The critical step the original run skipped: bake real glyphs into the dynamic
            // atlas NOW instead of leaving an empty 0x0 texture and hoping runtime demand
            // fills it before anyone looks.
            bool allAdded = fontAsset.TryAddCharacters(BakeSet, out string missing);
            if (!allAdded && !string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[FontSetup] {missing.Length} character(s) missing from the face: '{missing}'");

            foreach (var tex in fontAsset.atlasTextures)
                if (tex != null) EditorUtility.SetDirty(tex);
            if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);
            EditorUtility.SetDirty(fontAsset);

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

            AssetDatabase.SaveAssets();

            // Trust the disk, not the in-memory object (the 07-21 lesson): parse the saved
            // YAML and prove the atlas actually persisted with real pixels and a full set.
            VerifyOnDisk();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FontSetup] FAILED: {e}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    private static void VerifyOnDisk()
    {
        string yaml = File.ReadAllText(AssetOut);
        int chars = Regex.Matches(yaml, "m_Unicode: ").Count;
        var data = Regex.Match(yaml, @"_typelessdata: ([0-9a-f]+)");
        if (!data.Success)
            throw new System.Exception("saved asset has NO embedded atlas pixel data — the dead-atlas bug is back");
        string hex = data.Groups[1].Value;
        int nonZero = 0;
        for (int i = 0; i < hex.Length; i += 2)
            if (hex[i] != '0' || hex[i + 1] != '0') nonZero++;
        float pct = 100f * nonZero / (hex.Length / 2);
        if (chars < 90 || pct < 5f)
            throw new System.Exception($"saved asset unhealthy: {chars} characters, atlas {pct:F1}% non-zero — expected >=90 chars and a populated atlas");
        Debug.Log($"[FontSetup] SUCCESS — Russo One SDF verified ON DISK: {chars} baked characters, " +
                  $"{hex.Length / 2} atlas bytes ({pct:F1}% non-zero), set as TMP default.");
    }
}
