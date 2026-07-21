using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Data-driven crowd-skin catalog + persistence (HOD Group B item 3). Eight slots:
    /// 1 default + 7 purchasable on the locked 1500→20000 curve. Content is currently
    /// color-variant skins — the earmarked Kenney retro-characters pack is NOT on disk
    /// (character-drop/ is empty), so colors stand in; the list being data-driven means the
    /// art drops in later without touching store/reward code.
    /// </summary>
    public static class SkinSystem
    {
        public class SkinDef
        {
            public string Name;
            public int Cost;
            public Color Color;
        }

        // Index 0 = default, always owned. Order defines store slots.
        public static readonly List<SkinDef> Skins = new List<SkinDef>
        {
            new SkinDef { Name = "CLASSIC",  Cost = 0,     Color = new Color(0.2314f, 0.4863f, 1f) }, // brand blue
            new SkinDef { Name = "MINT",     Cost = 1500,  Color = new Color(0.24f, 0.83f, 0.55f) },
            new SkinDef { Name = "SUNSET",   Cost = 3000,  Color = new Color(1f, 0.62f, 0.26f) },
            new SkinDef { Name = "BERRY",    Cost = 5000,  Color = new Color(0.78f, 0.29f, 0.85f) },
            new SkinDef { Name = "GOLD",     Cost = 8000,  Color = new Color(1f, 0.78f, 0.16f) },
            new SkinDef { Name = "CRIMSON",  Cost = 12000, Color = new Color(0.92f, 0.2f, 0.29f) },
            new SkinDef { Name = "MIDNIGHT", Cost = 16000, Color = new Color(0.16f, 0.19f, 0.38f) },
            new SkinDef { Name = "PEARL",    Cost = 20000, Color = new Color(0.94f, 0.94f, 0.98f) },
        };

        private const string OwnedKeyPrefix = "skin_owned_";
        private const string EquippedKey = "skin_equipped";

        public static bool IsOwned(int i) => i == 0 || PlayerPrefs.GetInt(OwnedKeyPrefix + i, 0) == 1;
        public static int EquippedIndex => Mathf.Clamp(PlayerPrefs.GetInt(EquippedKey, 0), 0, Skins.Count - 1);

        public static void GrantSkin(int i)
        {
            if (i <= 0 || i >= Skins.Count) return;
            PlayerPrefs.SetInt(OwnedKeyPrefix + i, 1);
            PlayerPrefs.Save();
        }

        public static bool TryBuy(int i)
        {
            if (i <= 0 || i >= Skins.Count || IsOwned(i)) return false;
            if (ScoreManager.Instance == null || !ScoreManager.Instance.TrySpend(Skins[i].Cost)) return false;
            GrantSkin(i);
            return true;
        }

        public static void Equip(int i)
        {
            if (!IsOwned(i)) return;
            PlayerPrefs.SetInt(EquippedKey, i);
            PlayerPrefs.Save();
        }

        /// <summary>Cheapest not-yet-owned skin index, or -1 when everything is owned.
        /// Used by the every-5th-run reward box.</summary>
        public static int CheapestLockedIndex()
        {
            int best = -1;
            for (int i = 1; i < Skins.Count; i++)
                if (!IsOwned(i) && (best < 0 || Skins[i].Cost < Skins[best].Cost))
                    best = i;
            return best;
        }

        /// <summary>Tint a crowd unit's renderers to the equipped skin. Skipped entirely for
        /// the default skin so the stock shared material stays untouched.</summary>
        public static void ApplyTo(GameObject unit)
        {
            int idx = EquippedIndex;
            if (idx == 0) return;
            Color c = Skins[idx].Color;
            foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
                if (r.material.HasProperty("_Color")) r.material.color = c;
        }
    }
}
