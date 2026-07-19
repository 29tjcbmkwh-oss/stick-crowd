using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Core
{
    /// <summary>
    /// ArcField Studios boot splash. Purely code-driven (no scene wiring, no logo asset) so it
    /// works regardless of which scene is first in the build: it spawns itself before the first
    /// scene loads, overlays a full-screen black canvas above everything (sortingOrder 32767),
    /// and fades the studio name in and out while the game underneath initializes as normal.
    /// </summary>
    public static class SplashScreen
    {
        private const float FadeInSeconds = 0.7f;
        private const float HoldSeconds = 1.0f;
        private const float FadeOutSeconds = 0.6f;

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

            var backgroundGo = new GameObject("Background", typeof(Image));
            backgroundGo.transform.SetParent(root.transform, false);
            var background = backgroundGo.GetComponent<Image>();
            background.color = Color.black;
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var labelGo = new GameObject("StudioName", typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "ArcField Studios";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 72;
            label.fontStyle = FontStyles.Normal;
            label.characterSpacing = 6f;
            label.color = Color.white;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(1600, 300);
            labelRect.anchoredPosition = Vector2.zero;

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var runner = root.AddComponent<SplashRunner>();
            runner.StartCoroutine(runner.Run(group, root));
        }

        private class SplashRunner : MonoBehaviour
        {
            public IEnumerator Run(CanvasGroup group, GameObject root)
            {
                yield return group.DOFade(1f, FadeInSeconds).SetUpdate(true).WaitForCompletion();
                yield return new WaitForSecondsRealtime(HoldSeconds);
                yield return group.DOFade(0f, FadeOutSeconds).SetUpdate(true).WaitForCompletion();
                Destroy(root);
            }
        }
    }
}
