using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.Utilities;

namespace _Scripts.Core
{
    /// <summary>
    /// ArcField Studios boot splash — Ali's direct spec (2026-07-23): a classy production-logo
    /// reveal. The wordmark pops in with an ease-out-back scale (bounce, not linear), holds
    /// ~1.3s, then the whole splash fades out cleanly. Purely code-driven (no scene wiring, no
    /// logo asset) so it works regardless of which scene is first in the build: it spawns
    /// itself before the first scene loads and overlays everything (sortingOrder 32767) while
    /// the game underneath initializes as normal. Uses the project TMP default font (Russo One
    /// once FontSetup has run) and the brand palette.
    /// </summary>
    public static class SplashScreen
    {
        private const float PopSeconds = 0.55f;
        private const float HoldSeconds = 1.3f;
        private const float FadeOutSeconds = 0.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Show()
        {
            var root = new GameObject("ArcFieldSplashScreen");
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Opaque from frame one — the reveal is the wordmark popping, not the backdrop.
            var backgroundGo = new GameObject("Background", typeof(Image));
            backgroundGo.transform.SetParent(root.transform, false);
            var background = backgroundGo.GetComponent<Image>();
            background.color = new Color(0.059f, 0.078f, 0.125f, 1f); // brand dark navy
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            // Logo lockup: big wordmark + letter-spaced sub-line + brand-blue rule between
            // them. One transform so the whole lockup pops as a unit.
            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(root.transform, false);
            var logoRect = (RectTransform)logoGo.transform;
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.sizeDelta = new Vector2(1000, 400);
            logoRect.anchoredPosition = Vector2.zero;

            var wordmark = MakeLabel(logoGo.transform, "ARCFIELD", 118, Color.white, new Vector2(0, 60));
            wordmark.fontStyle = FontStyles.Bold;
            wordmark.characterSpacing = 4f;

            var ruleGo = new GameObject("Rule", typeof(Image));
            ruleGo.transform.SetParent(logoGo.transform, false);
            var rule = ruleGo.GetComponent<Image>();
            rule.color = BrandPalette.Blue;
            var ruleRect = rule.rectTransform;
            ruleRect.anchorMin = new Vector2(0.5f, 0.5f);
            ruleRect.anchorMax = new Vector2(0.5f, 0.5f);
            ruleRect.sizeDelta = new Vector2(560, 6);
            ruleRect.anchoredPosition = new Vector2(0, -18);

            var subline = MakeLabel(logoGo.transform, "STUDIOS", 46, BrandPalette.Blue, new Vector2(0, -78));
            subline.characterSpacing = 34f;

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 1f;

            var runner = root.AddComponent<SplashRunner>();
            runner.StartCoroutine(runner.Run(logoRect, group, root));
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, string text, float size,
                                                 Color color, Vector2 pos)
        {
            var go = new GameObject(text, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000, 160);
            rect.anchoredPosition = pos;
            return label;
        }

        private class SplashRunner : MonoBehaviour
        {
            public IEnumerator Run(RectTransform logo, CanvasGroup group, GameObject root)
            {
                // ease-out-back: overshoots ~1.1x then settles — the "bounce" of the spec.
                logo.localScale = Vector3.zero;
                yield return logo.DOScale(1f, PopSeconds)
                    .SetEase(Ease.OutBack, 1.4f)
                    .SetUpdate(true)
                    .WaitForCompletion();
                yield return new WaitForSecondsRealtime(HoldSeconds);
                yield return group.DOFade(0f, FadeOutSeconds).SetUpdate(true).WaitForCompletion();
                Destroy(root);
            }
        }
    }
}
