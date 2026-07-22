// Runs marker-requested work from a GUI editor without menu interaction. Markers at the
// project root:
//   apply-theme-request       -> run the Blue-vs-Orange theme pass
//   visual-overhaul-request   -> run the gate/sky/lighting visual pass
//   post-processing-request   -> apply the Bloom/ColorAdjustments/Vignette/AO volume
//   font-setup-request        -> import Russo One and set it as the TMP default font
//   autobuild-request         -> build the Android debug APK
//   autobuild-release-request -> build the signed Android release AAB
//   capture-gameplay-request  -> handled by GameplayCapture.cs (own polling loop)
//
// Uses EditorApplication.update (re-subscribed on every domain reload) rather than
// delayCall: importing the AdMob SDK / EDM4U triggers repeated domain reloads that DISCARD
// pending delayCalls before they fire, so the build never started. Polling update survives
// reloads and fires only once the editor is idle (not compiling/updating).
//
// Deliberately stays subscribed forever instead of a one-shot latch: an earlier version set
// a static `_fired` flag and unsubscribed after the first marker it handled, which meant every
// marker after the first silently did nothing until the next script recompile reset the static
// field via domain reload (discovered 2026-07-20 — two markers sat unconsumed for several
// minutes with no error, because nothing was listening anymore). `_busy` only guards against
// re-entering while a request is actively being processed in the same tick.
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    private const string BuildMarker   = "/Users/a/blue-vs-orange-runner/base/autobuild-request";
    private const string ReleaseMarker = "/Users/a/blue-vs-orange-runner/base/autobuild-release-request";
    private const string ThemeMarker   = "/Users/a/blue-vs-orange-runner/base/apply-theme-request";
    private const string VisualMarker  = "/Users/a/blue-vs-orange-runner/base/visual-overhaul-request";
    private const string CharMarker    = "/Users/a/blue-vs-orange-runner/base/character-swap-request";
    private const string KenneyMarker  = "/Users/a/blue-vs-orange-runner/base/kenney-swap-request";
    private const string PostFxMarker  = "/Users/a/blue-vs-orange-runner/base/post-processing-request";
    private const string FontMarker    = "/Users/a/blue-vs-orange-runner/base/font-setup-request";
    private static bool _busy;

    static AutoBuildTrigger()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (_busy) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        var theme   = File.Exists(ThemeMarker);
        var visual  = File.Exists(VisualMarker);
        var chr     = File.Exists(CharMarker);
        var kenney  = File.Exists(KenneyMarker);
        var build   = File.Exists(BuildMarker);
        var release = File.Exists(ReleaseMarker);
        var postFx  = File.Exists(PostFxMarker);
        var font    = File.Exists(FontMarker);
        if (!theme && !visual && !chr && !kenney && !build && !release && !postFx && !font) return;

        _busy = true;
        try
        {
            if (theme)
            {
                File.Delete(ThemeMarker);
                Debug.Log("[AutoBuildTrigger] theme marker found — applying theme");
                ThemeSetup.Run();
            }
            if (visual)
            {
                File.Delete(VisualMarker);
                Debug.Log("[AutoBuildTrigger] visual marker found — applying visual overhaul");
                VisualOverhaul.Run();
            }
            if (postFx)
            {
                File.Delete(PostFxMarker);
                Debug.Log("[AutoBuildTrigger] post-processing marker found — applying volume");
                PostProcessingSetup.Run();
            }
            if (font)
            {
                File.Delete(FontMarker);
                Debug.Log("[AutoBuildTrigger] font marker found — importing Russo One");
                FontSetup.Run();
            }
            if (chr)
            {
                File.Delete(CharMarker);
                Debug.Log("[AutoBuildTrigger] character marker found — swapping character");
                CharacterSwap.Run();
            }
            if (kenney)
            {
                File.Delete(KenneyMarker);
                Debug.Log("[AutoBuildTrigger] kenney marker found — swapping to Kenney character");
                KenneySwap.Run();
            }
            if (build)
            {
                File.Delete(BuildMarker);
                Debug.Log("[AutoBuildTrigger] build marker found — starting Android debug build");
                AndroidBuilder.BuildDebugApk();
            }
            if (release)
            {
                File.Delete(ReleaseMarker);
                Debug.Log("[AutoBuildTrigger] release marker found — starting Android release AAB build");
                ReleaseBuilder.BuildReleaseAab();
            }
        }
        finally
        {
            _busy = false;
        }
    }
}
