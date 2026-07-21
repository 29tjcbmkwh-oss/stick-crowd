using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Core
{
    /// <summary>
    /// 8-slot skin store (HOD Group B item 3): grid of SkinSystem entries, buy with coins,
    /// one equipped/highlighted. Code-built at runtime like RewardChoicePanel; opened from
    /// a start-screen button that UIManager wires up. Public static Open/Close so the
    /// capture pipeline can screenshot it headlessly.
    /// </summary>
    public static class SkinStorePanel
    {
        private static GameObject _panel;

        public static bool IsOpen => _panel != null;

        public static void Toggle(Transform canvasParent)
        {
            if (_panel != null) Close(); else Open(canvasParent);
        }

        public static void Close()
        {
            if (_panel != null) Object.Destroy(_panel);
            _panel = null;
        }

        public static void Open(Transform canvasParent)
        {
            if (_panel != null) return;
            _panel = new GameObject("SkinStorePanel", typeof(RectTransform));
            _panel.transform.SetParent(canvasParent, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _panel.transform.SetAsLastSibling();

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.059f, 0.078f, 0.125f, 0.98f);

            var title = MakeText(_panel.transform, "SKINS", 66, new Vector2(0, 700), new Vector2(600, 120));
            title.fontStyle = FontStyles.Bold;

            // 2 columns x 4 rows
            const float cellW = 380f, cellH = 300f;
            for (int i = 0; i < SkinSystem.Skins.Count; i++)
                BuildSlot(i, new Vector2((i % 2 == 0 ? -1 : 1) * (cellW * 0.55f),
                                          440 - (i / 2) * (cellH + 20)));

            MakeButton(_panel.transform, "CLOSE", new Color(0.92f, 0.25f, 0.25f, 1f),
                new Vector2(0, -780), new Vector2(420, 110), Close);
        }

        private static void BuildSlot(int index, Vector2 pos)
        {
            var skin = SkinSystem.Skins[index];
            bool owned = SkinSystem.IsOwned(index);
            bool equipped = SkinSystem.EquippedIndex == index;

            var slot = new GameObject("Slot", typeof(RectTransform));
            slot.transform.SetParent(_panel.transform, false);
            var rt = (RectTransform)slot.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(360, 290);

            var back = slot.AddComponent<Image>();
            back.color = equipped ? new Color(0.24f, 0.83f, 0.39f, 0.25f) : new Color(1f, 1f, 1f, 0.06f);
            if (UIManager.Instance != null && UIManager.Instance.coinSprite != null)
            { back.sprite = UIManager.Instance.coinSprite; back.type = Image.Type.Sliced; }

            // color swatch = the skin content preview
            var swatch = new GameObject("Swatch", typeof(RectTransform));
            swatch.transform.SetParent(slot.transform, false);
            var si = swatch.AddComponent<Image>();
            si.color = skin.Color;
            if (UIManager.Instance != null && UIManager.Instance.coinSprite != null)
            { si.sprite = UIManager.Instance.coinSprite; si.type = Image.Type.Sliced; }
            var srt = (RectTransform)swatch.transform;
            srt.anchoredPosition = new Vector2(0, 55); srt.sizeDelta = new Vector2(150, 130);

            MakeText(slot.transform, skin.Name, 34, new Vector2(0, -40), new Vector2(340, 50));

            string action = equipped ? "EQUIPPED" : owned ? "EQUIP" : $"{skin.Cost}";
            var btnColor = equipped ? new Color(0.24f, 0.83f, 0.39f, 1f)
                          : owned ? new Color(0.2314f, 0.4863f, 1f, 1f)
                          : new Color(1f, 0.78f, 0.16f, 1f);
            var btn = MakeButton(slot.transform, action, btnColor, new Vector2(0, -105),
                new Vector2(280, 78), () =>
                {
                    if (SkinSystem.IsOwned(index)) SkinSystem.Equip(index);
                    else SkinSystem.TryBuy(index);
                    // rebuild to refresh all slot states
                    var parent = _panel.transform.parent;
                    Close(); Open(parent);
                });
            if (equipped) btn.GetComponent<Button>().interactable = false;
        }

        private static TMP_Text MakeText(Transform parent, string text, float size, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white; t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = sz;
            return t;
        }

        private static GameObject MakeButton(Transform parent, string label, Color color,
            Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (UIManager.Instance != null && UIManager.Instance.coinSprite != null)
            { img.sprite = UIManager.Instance.coinSprite; img.type = Image.Type.Sliced; }
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = MakeText(go.transform, label, 34, Vector2.zero, size);
            t.fontStyle = FontStyles.Bold;
            return go;
        }
    }
}
