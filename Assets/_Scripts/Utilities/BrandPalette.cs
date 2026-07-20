using UnityEngine;

namespace _Scripts.Utilities
{
    /// <summary>
    /// Single source of truth for the Blue vs Orange Runner brand palette. Compiles into
    /// both Editor tools (ThemeSetup, VisualOverhaul) and runtime code (GameJuice, HUD),
    /// so the two "choice" colors are defined exactly once instead of as separate inline
    /// Color(...) literals per script (previously three near-duplicate blues, and the
    /// negative choice was still Red despite the game being named "Blue vs Orange").
    /// </summary>
    public static class BrandPalette
    {
        // The only two gameplay-decision colors. Blue = safe/positive choice,
        // Orange = danger/alternative choice. Nothing else in the HUD should compete with these.
        public static readonly Color Blue   = new Color(0.180f, 0.420f, 1.000f);
        public static readonly Color Orange = new Color(1.000f, 0.478f, 0.102f);

        // Translucent gate-material variants (same hues, alpha for the trigger-volume look).
        public static readonly Color BlueTranslucent   = new Color(Blue.r, Blue.g, Blue.b, 0.55f);
        public static readonly Color OrangeTranslucent = new Color(Orange.r, Orange.g, Orange.b, 0.55f);

        // Neutral surfaces/text shared by every panel (HUD, gate plates, game-over/win, leaderboard).
        public static readonly Color SurfaceDark   = new Color(0.059f, 0.078f, 0.125f, 0.82f); // card/panel backing
        public static readonly Color TextPrimary   = new Color(0.961f, 0.969f, 0.980f);        // near-white
        public static readonly Color TextMuted     = new Color(0.580f, 0.639f, 0.722f);        // secondary text
        public static readonly Color BorderLight   = new Color(1f, 1f, 1f, 0.10f);

        public static Color ForChoice(bool positive) => positive ? Blue : Orange;
        public static Color ForChoiceTranslucent(bool positive) => positive ? BlueTranslucent : OrangeTranslucent;
    }
}
