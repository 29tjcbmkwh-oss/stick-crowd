using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Core
{
    /// <summary>
    /// Code-built retention UI so the reusable base does not require brittle scene edits.
    /// Adds a persistent coin counter, first-run tutorial, rewarded revive and 2x win reward.
    /// </summary>
    public sealed class ProgressionUI : MonoBehaviour
    {
        private const string TutorialSeenKey = "stickcrowd.tutorial_seen";
        private Text _coinText;
        private Text _tutorialText;
        private Text _actionText;
        private Text _statusText;
        private Button _actionButton;
        private GameState _state;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<GameFlowManager>() == null || FindObjectOfType<ProgressionUI>() != null) return;
            new GameObject("[ProgressionUI]").AddComponent<ProgressionUI>();
        }

        private IEnumerator Start()
        {
            yield return null;
            BuildUi();
            HideCornerClutter();
            GameFlowManager.onGameStateChange += HandleGameState;
            ScoreManager.CoinsChanged += HandleCoinsChanged;
            HandleCoinsChanged(ScoreManager.Instance != null ? ScoreManager.Instance.Coins : 0);
            if (GameFlowManager.Instance != null) HandleGameState(GameFlowManager.Instance.state);
        }

        private void OnDestroy()
        {
            GameFlowManager.onGameStateChange -= HandleGameState;
            ScoreManager.CoinsChanged -= HandleCoinsChanged;
        }

        private void Update()
        {
            if (_tutorialText != null && _tutorialText.gameObject.activeSelf &&
                _state == GameState.Game && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
                CompleteTutorial();
        }

        // Position-based, name-agnostic: hide any template image sitting in the extreme top-left
        // or top-right corner (the leftover boxes), while never touching my own HUD. Robust to
        // whatever those objects are actually named — hiding by name proved unreliable.
        private void HideCornerClutter()
        {
            var corners = new Vector3[4];
            foreach (Image img in FindObjectsOfType<Image>())
            {
                if (img.transform.IsChildOf(transform)) continue;   // never touch my own HUD
                img.rectTransform.GetWorldCorners(corners);          // screen pixels for overlay canvas
                Vector3 c = (corners[0] + corners[2]) * 0.5f;
                float x = c.x / Screen.width, y = c.y / Screen.height;
                if (y > 0.85f && (x < 0.30f || x > 0.70f))
                {
                    img.gameObject.SetActive(false);
                    Debug.Log($"[ProgressionUI] hid corner box: {img.name} (x={x:F2} y={y:F2})");
                }
            }
        }

        private void BuildUi()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Gold coin pill, top-left, on its own dark background so it always reads.
            GameObject coinPill = new GameObject("CoinPill", typeof(RectTransform), typeof(Image));
            coinPill.transform.SetParent(transform, false);
            coinPill.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.11f, 0.85f);
            SetRect((RectTransform)coinPill.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(210, -70), new Vector2(340, 90));

            GameObject coinDot = new GameObject("CoinDot", typeof(RectTransform), typeof(Image));
            coinDot.transform.SetParent(coinPill.transform, false);
            coinDot.GetComponent<Image>().color = new Color(1f, 0.82f, 0.25f);
            SetRect((RectTransform)coinDot.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(50, 0), new Vector2(54, 54));

            _coinText = CreateText("Coins", coinPill.transform, 44, TextAnchor.MiddleLeft);
            _coinText.color = new Color(1f, 0.86f, 0.36f);
            SetRect(_coinText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            _coinText.rectTransform.offsetMin = new Vector2(92, 0);
            _coinText.rectTransform.offsetMax = new Vector2(-16, 0);

            _tutorialText = CreateText("Tutorial", transform, 38, TextAnchor.MiddleCenter);
            _tutorialText.text = "SWIPE TO MOVE  •  CHOOSE THE BEST GATE";
            _tutorialText.color = new Color(0.65f, 1f, 0.95f, 1f);
            SetRect(_tutorialText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 260), new Vector2(920, 100));

            GameObject buttonObject = new GameObject("RewardedAction", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.04f, 0.58f, 0.45f, 0.98f);
            _actionButton = buttonObject.GetComponent<Button>();
            _actionButton.targetGraphic = image;
            _actionButton.onClick.AddListener(HandleActionClicked);
            SetRect((RectTransform)buttonObject.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 205), new Vector2(600, 105));

            _actionText = CreateText("Label", buttonObject.transform, 34, TextAnchor.MiddleCenter);
            SetRect(_actionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _actionText.rectTransform.offsetMin = Vector2.zero;
            _actionText.rectTransform.offsetMax = Vector2.zero;

            _statusText = CreateText("Status", transform, 25, TextAnchor.MiddleCenter);
            _statusText.color = new Color(1f, 0.85f, 0.4f, 1f);
            SetRect(_statusText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 145), new Vector2(850, 55));

            _actionButton.gameObject.SetActive(false);
            _statusText.gameObject.SetActive(false);
            _tutorialText.gameObject.SetActive(false);
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void HandleCoinsChanged(int balance)
        {
            if (_coinText != null) _coinText.text = balance.ToString();
        }

        private void HandleGameState(GameState state)
        {
            _state = state;
            HideCornerClutter();   // re-assert in case the template re-enabled them
            if (_actionButton == null) return;

            _actionButton.interactable = true;
            _statusText.gameObject.SetActive(false);
            _actionButton.gameObject.SetActive(false);

            if (state == GameState.Game && PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0)
                _tutorialText.gameObject.SetActive(true);
            else if (state != GameState.Start)
                _tutorialText.gameObject.SetActive(false);

            if (state == GameState.Lose && GameFlowManager.Instance.CanRevive)
            {
                _actionText.text = "SECOND CHANCE  •  WATCH AD";
                _actionButton.gameObject.SetActive(true);
            }
            else if (state == GameState.Win && GameFlowManager.Instance.CanDoubleReward)
            {
                _actionText.text = "2× COINS  •  WATCH AD";
                _actionButton.gameObject.SetActive(true);
            }
        }

        private void CompleteTutorial()
        {
            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            PlayerPrefs.Save();
            _tutorialText.gameObject.SetActive(false);
        }

        private void HandleActionClicked()
        {
            _actionButton.interactable = false;
            _statusText.text = "LOADING REWARDED AD…";
            _statusText.gameObject.SetActive(true);

            if (_state == GameState.Lose)
                GameFlowManager.Instance.TryRevive(HandleRewardResult);
            else if (_state == GameState.Win)
                GameFlowManager.Instance.TryDoubleWinReward(HandleRewardResult);
        }

        private void HandleRewardResult(bool success)
        {
            if (success)
            {
                _actionButton.gameObject.SetActive(false);
                _statusText.gameObject.SetActive(false);
                HandleCoinsChanged(ScoreManager.Instance != null ? ScoreManager.Instance.Coins : 0);
                return;
            }

            _actionButton.interactable = true;
            _statusText.text = "AD NOT READY — PLEASE TRY AGAIN";
        }
    }
}
