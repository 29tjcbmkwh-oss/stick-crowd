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
        // Values match "Visual Reskin Spec" exactly: #3B7CFF / #FF7A2F.
        public static readonly Color Blue   = new Color(0.2314f, 0.4863f, 1.0000f);
        public static readonly Color Orange = new Color(1.0000f, 0.4784f, 0.1843f);

        // Gate-material alpha RAISED 0.45 -> 0.82 (HOD 2026-07-20, from the first real gameplay
        // capture). At 45% over a near-white environment the brand hues composited toward white
        // and the gates rendered as pale cyan / pale lemon — the two colours the game is *named
        // after* were effectively absent. Alpha this high still reads as glass against a
        // saturated sky, but keeps the hue identity intact. Do not lower without re-capturing.
        public static readonly Color BlueTranslucent   = new Color(Blue.r, Blue.g, Blue.b, 0.82f);
        public static readonly Color OrangeTranslucent = new Color(Orange.r, Orange.g, Orange.b, 0.82f);

        // Environment palette — CORRECTED 2026-07-20 after the first gameplay capture showed a
        // white-on-white "blank page": track #EDEFF4 against sky/fog #FFFFFF is a ~7% luminance
        // difference, i.e. effectively zero contrast, so the track was invisible. The reference
        // (Count Masters) does NOT use a white void — it puts a PALE track against a SATURATED
        // BLUE environment. That single relationship is what makes the track read, the crowd pop,
        // and the gates hold their colour. Sky is now real blue; track stays pale but is now the
        // light element against a dark-enough field, with a much stronger lane edge to define it.
        public static readonly Color SkyTop      = new Color(0.1176f, 0.4980f, 0.8784f); // #1E7FE0 deep sky
        public static readonly Color SkyHorizon  = new Color(0.4980f, 0.7686f, 1.0000f); // #7FC4FF light horizon
        public static readonly Color GroundLight = new Color(0.9294f, 0.9373f, 0.9569f); // #EDEFF4 pale track
        public static readonly Color LaneEdge    = new Color(0.2314f, 0.4863f, 1.0000f); // brand blue, defines track edge

        // Neutral surfaces/text shared by every panel (HUD, gate plates, game-over/win, leaderboard).
        public static readonly Color SurfaceDark   = new Color(0.059f, 0.078f, 0.125f, 0.82f); // card/panel backing
        public static readonly Color TextPrimary   = new Color(0.961f, 0.969f, 0.980f);        // near-white
        public static readonly Color TextMuted     = new Color(0.580f, 0.639f, 0.722f);        // secondary text
        public static readonly Color BorderLight   = new Color(1f, 1f, 1f, 0.10f);

        public static Color ForChoice(bool positive) => positive ? Blue : Orange;
        public static Color ForChoiceTranslucent(bool positive) => positive ? BlueTranslucent : OrangeTranslucent;
    }
}
