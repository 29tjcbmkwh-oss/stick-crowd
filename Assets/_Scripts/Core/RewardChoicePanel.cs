using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.Ads;

namespace _Scripts.Core
{
    /// <summary>
    /// Win-path reward-choice screen (HOD Group B item 1, locked spec): N mystery boxes
    /// (configurable payout array, default 0.5/1/2x run reward), values hidden until the
    /// pick, losers revealed after, every 5th completion box C holds the cheapest locked
    /// skin, and a rewarded-video "open a second box" hook after the reveal. Fully
    /// code-built at runtime (LeaderboardPanel pattern) — no scene/prefab YAML edits.
    /// </summary>
    public static class RewardChoicePanel
    {
        // N-configurable by design: box count follows this array's length.
        public static float[] PayoutMultipliers = { 0.5f, 1f, 2f };

        private const string CompletionCountKey = "run_completions";

        public static void Show(Transform canvasParent, Action onDone)
        {
            int completions = PlayerPrefs.GetInt(CompletionCountKey, 0) + 1;
            PlayerPrefs.SetInt(CompletionCountKey, completions);
            PlayerPrefs.Save();

            int runReward = ScoreManager.Instance != null ? ScoreManager.Instance.LastRunReward : 100;
            int n = PayoutMultipliers.Length;

            // prize per box: coins by payout multiplier; every 5th completion the LAST box
            // carries the cheapest locked skin instead (spec: "box C")
            int skinPrizeBox = -1;
            int skinIndex = -1;
            if (completions % 5 == 0)
            {
                skinIndex = SkinSystem.CheapestLockedIndex();
                if (skinIndex > 0) skinPrizeBox = n - 1;
            }

            var root = new GameObject("RewardChoicePanel", typeof(RectTransform));
            root.transform.SetParent(canvasParent, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.059f, 0.078f, 0.125f, 0.98f);

            var title = MakeText(root.transform, "PICK A REWARD", 64, new Vector2(0, 620));
            title.fontStyle = FontStyles.Bold;

            var chosen = new bool[1] { false };
            int opened = 0;
            var boxes = new Button[n];
            var boxLabels = new TMP_Text[n];

            Action<int> revealAll = null;
            GameObject adButton = null, continueButton = null;

            Action<int> openBox = pick =>
            {
                opened++;
                boxLabels[pick].text = PrizeText(pick, skinPrizeBox, skinIndex, runReward);
                boxLabels[pick].fontSize = 40;
                boxes[pick].image.color = new Color(0.24f, 0.83f, 0.39f, 1f);
                boxes[pick].interactable = false;
                GrantPrize(pick, skinPrizeBox, skinIndex, runReward);
            };

            revealAll = pick =>
            {
                openBox(pick);
                for (int i = 0; i < n; i++)
                {
                    if (boxes[i].interactable)
                    {
                        boxLabels[i].text = PrizeText(i, skinPrizeBox, skinIndex, runReward);
                        boxLabels[i].fontSize = 36;
                        var img = boxes[i].image;
                        img.color = new Color(img.color.r, img.color.g, img.color.b, 0.45f);
                        boxes[i].interactable = false;
                    }
                }
                // rewarded-video second box (spec): only if an ad is actually ready
                if (AdManager.Instance != null && AdManager.Instance.IsRewardedReady)
                {
                    int secondPick = (pick + 1) % n;
                    adButton = MakeButton(root.transform, "OPEN A 2ND BOX • WATCH AD",
                        new Color(0.05f, 0.55f, 0.45f, 1f), new Vector2(0, -520), new Vector2(760, 110),
                        () =>
                        {
                            adButton.GetComponent<Button>().interactable = false;
                            AdManager.Instance.ShowRewarded(
                                () => { boxes[secondPick].interactable = true; openBox(secondPick); },
                                () => { if (adButton != null) adButton.GetComponent<Button>().interactable = true; });
                        });
                }
                continueButton = MakeButton(root.transform, "CONTINUE",
                    new Color(0.24f, 0.83f, 0.39f, 1f), new Vector2(0, -680), new Vector2(560, 130),
                    () => { UnityEngine.Object.Destroy(root); onDone?.Invoke(); });
            };

            float spacing = 340f;
            float x0 = -spacing * (n - 1) / 2f;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var b = MakeBoxButton(root.transform, new Vector2(x0 + spacing * i, 60),
                    () => { if (!chosen[0]) { chosen[0] = true; revealAll(idx); } });
                boxes[i] = b.Item1;
                boxLabels[i] = b.Item2;
                b.Item1.transform.localScale = Vector3.zero;
                b.Item1.transform.DOScale(1f, 0.35f).SetDelay(0.1f * i).SetEase(Ease.OutBack);
            }
        }

        private static string PrizeText(int box, int skinBox, int skinIndex, int runReward)
        {
            if (box == skinBox) return $"SKIN\n{SkinSystem.Skins[skinIndex].Name}";
            return $"+{Mathf.RoundToInt(runReward * PayoutMultipliers[box])}";
        }

        private static void GrantPrize(int box, int skinBox, int skinIndex, int runReward)
        {
            if (box == skinBox) { SkinSystem.GrantSkin(skinIndex); return; }
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.GrantBonusCoins(Mathf.RoundToInt(runReward * PayoutMultipliers[box]));
        }

        private static TMP_Text MakeText(Transform parent, string text, float size, Vector2 pos)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white; t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(1000, 140);
            return t;
        }

        private static GameObject MakeButton(Transform parent, string label, Color color,
            Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (UIManager.Instance != null && UIManager.Instance.coinSprite != null)
            { img.sprite = UIManager.Instance.coinSprite; img.type = Image.Type.Sliced; }
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = MakeText(go.transform, label, 40, Vector2.zero);
            t.fontStyle = FontStyles.Bold;
            ((RectTransform)t.transform).sizeDelta = size;
            return go;
        }

        private static (Button, TMP_Text) MakeBoxButton(Transform parent, Vector2 pos, Action onClick)
        {
            var go = MakeButton(parent, "?", new Color(0.2314f, 0.4863f, 1f, 1f), pos,
                new Vector2(300, 340), onClick);
            var label = go.GetComponentInChildren<TMP_Text>();
            label.fontSize = 110;
            label.fontStyle = FontStyles.Bold;
            return (go.GetComponent<Button>(), label);
        }
    }
}
