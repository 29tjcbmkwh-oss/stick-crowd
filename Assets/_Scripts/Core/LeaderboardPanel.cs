using System.Collections.Generic;
using Crystal;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Core
{
    /// <summary>
    /// Builds and drives the leaderboard modal entirely at runtime, so this feature ships
    /// without hand-edited prefab/scene changes to the existing Canvas. Visual language
    /// (scale-pop open, rounded cards, dark surfaces with a Blue-vs-Orange accent) is chosen
    /// to sit next to the game's existing GameOver/GameWin popups without looking bolted-on.
    /// Open/Close reuse UIManager.ActivatePopup/ClosePopup so pause, haptics and SFX stay
    /// identical to every other popup in the game.
    /// </summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        const float RowHeight = 92f;
        const float RowSpacing = 10f;
        const float MinLoadingSeconds = 0.45f; // avoids an instant flash; matches future async latency

        static readonly Color ColorScrim = new Color(0f, 0f, 0f, 0.62f);
        static readonly Color ColorCardTop = new Color32(0x18, 0x1F, 0x3D, 0xFF);
        static readonly Color ColorBlue = new Color32(0x3B, 0x7C, 0xFF, 0xFF);
        static readonly Color ColorOrange = new Color32(0xFF, 0x7A, 0x2F, 0xFF);
        static readonly Color ColorRow = new Color32(0x1C, 0x22, 0x42, 0xFF);
        static readonly Color ColorRowAlt = new Color32(0x21, 0x28, 0x4A, 0xFF);
        static readonly Color ColorTextPrimary = new Color32(0xF1, 0xF4, 0xFF, 0xFF);
        static readonly Color ColorTextSecondary = new Color32(0x8B, 0x93, 0xBC, 0xFF);
        static readonly Color ColorGold = new Color32(0xFF, 0xC9, 0x4A, 0xFF);
        static readonly Color ColorSilver = new Color32(0xC9, 0xD3, 0xE0, 0xFF);
        static readonly Color ColorBronze = new Color32(0xE2, 0x96, 0x5A, 0xFF);

        Sprite _roundedSprite;
        Sprite _circleSprite;

        RectTransform _card;
        RectTransform _scrollViewport;
        RectTransform _content;
        ScrollRect _scrollRect;

        GameObject _loadingGroup;
        GameObject _emptyGroup;
        GameObject _errorGroup;
        GameObject _listGroup;

        readonly List<GameObject> _spawnedRows = new List<GameObject>();
        int _pendingRequestId;

        public static LeaderboardPanel Build(Transform canvasRoot)
        {
            var go = new GameObject("LeaderboardPanel", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var panel = go.AddComponent<LeaderboardPanel>();
            panel.Construct();
            go.SetActive(false);
            go.transform.localScale = Vector3.zero;
            return panel;
        }

        void Construct()
        {
            // GetBuiltinResource<Sprite>("UI/Skin/...") THROWS in this Unity build ("Failed
            // to find UI/Skin/Knob.psd") — it killed UIManager.Start at the Build() call for
            // the entire life of this panel, which is why the leaderboard (and everything
            // built after it in Start) never appeared. The UIManager-supplied rounded sprite
            // works; null just renders plain quads, which is fine for the scrim/rows.
            _roundedSprite = UIManager.Instance != null ? UIManager.Instance.coinSprite : null;
            _circleSprite = _roundedSprite;

            var root = (RectTransform)transform;
            Stretch(root);

            // Scrim — tap outside to close.
            var scrimGO = CreateImage("Scrim", root, ColorScrim, null);
            Stretch((RectTransform)scrimGO.transform);
            var scrimButton = scrimGO.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(RequestClose);

            // Safe-area root so the card never sits under a notch / gesture bar.
            var safeAreaGO = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaGO.transform.SetParent(root, false);
            Stretch((RectTransform)safeAreaGO.transform);
            safeAreaGO.AddComponent<SafeArea>();

            // Card.
            var cardGO = CreateImage("Card", safeAreaGO.transform, ColorCardTop, _roundedSprite);
            _card = (RectTransform)cardGO.transform;
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(940, 1500);
            _card.anchoredPosition = Vector2.zero;
            AddSubtleGradientWash(_card);

            var cardLayout = cardGO.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(36, 36, 32, 32);
            cardLayout.spacing = 18;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            BuildHeader(_card);
            BuildDivider(_card);
            BuildContentArea(_card);
        }

        void BuildHeader(Transform parent)
        {
            var headerGO = new GameObject("Header", typeof(RectTransform));
            headerGO.transform.SetParent(parent, false);
            var headerRt = (RectTransform)headerGO.transform;
            headerRt.sizeDelta = new Vector2(0, 96);
            var headerLayout = headerGO.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 96;
            headerLayout.flexibleWidth = 1;

            var titleGO = CreateText("Title", headerGO.transform, "LEADERBOARD", 44, FontStyles.Bold, ColorTextPrimary, TextAlignmentOptions.TopLeft);
            var titleRt = (RectTransform)titleGO.transform;
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0, 1);
            titleRt.anchoredPosition = new Vector2(0, 0);
            titleRt.sizeDelta = new Vector2(-90, 56);
            var titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
            titleTmp.characterSpacing = 2;

            var subtitleGO = CreateText("Subtitle", headerGO.transform, "Local competitive board", 24, FontStyles.Normal, ColorTextSecondary, TextAlignmentOptions.TopLeft);
            var subtitleRt = (RectTransform)subtitleGO.transform;
            subtitleRt.anchorMin = new Vector2(0, 1);
            subtitleRt.anchorMax = new Vector2(1, 1);
            subtitleRt.pivot = new Vector2(0, 1);
            subtitleRt.anchoredPosition = new Vector2(0, -54);
            subtitleRt.sizeDelta = new Vector2(-90, 34);

            // Close button, top-right, 44x44+ touch target.
            var closeGO = CreateImage("CloseButton", headerGO.transform, new Color(1, 1, 1, 0.08f), _circleSprite);
            var closeRt = (RectTransform)closeGO.transform;
            closeRt.anchorMin = new Vector2(1, 1);
            closeRt.anchorMax = new Vector2(1, 1);
            closeRt.pivot = new Vector2(1, 1);
            closeRt.sizeDelta = new Vector2(64, 64);
            closeRt.anchoredPosition = new Vector2(0, 0);
            var closeButton = closeGO.AddComponent<Button>();
            var closeColors = closeButton.colors;
            closeColors.highlightedColor = new Color(1, 1, 1, 0.16f);
            closeColors.pressedColor = new Color(1, 1, 1, 0.24f);
            closeButton.colors = closeColors;
            closeButton.onClick.AddListener(RequestClose);
            var closeLabel = CreateText("X", closeGO.transform, "X", 28, FontStyles.Bold, ColorTextPrimary, TextAlignmentOptions.Center);
            Stretch((RectTransform)closeLabel.transform);
        }

        void BuildDivider(Transform parent)
        {
            var dividerGO = CreateImage("Divider", parent, new Color(1, 1, 1, 0.08f), null);
            var layout = dividerGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.flexibleWidth = 1;
        }

        void BuildContentArea(Transform parent)
        {
            var contentAreaGO = new GameObject("ContentArea", typeof(RectTransform));
            contentAreaGO.transform.SetParent(parent, false);
            var layout = contentAreaGO.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1;
            layout.flexibleWidth = 1;
            var areaRt = (RectTransform)contentAreaGO.transform;
            areaRt.sizeDelta = new Vector2(0, 900);

            _listGroup = BuildScrollList(contentAreaGO.transform);
            _loadingGroup = BuildLoadingState(contentAreaGO.transform);
            _emptyGroup = BuildMessageState(contentAreaGO.transform, "EmptyState", "No runs yet",
                "Finish a run to join the board", null);
            _errorGroup = BuildMessageState(contentAreaGO.transform, "ErrorState", "Couldn't load leaderboard",
                "Check your connection and try again", RequestRetry);
        }

        GameObject BuildScrollList(Transform parent)
        {
            var scrollGO = new GameObject("ListScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(parent, false);
            Stretch((RectTransform)scrollGO.transform);
            _scrollRect = scrollGO.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;
            _scrollRect.elasticity = 0.12f;
            _scrollRect.scrollSensitivity = 24f;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.135f;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            _scrollViewport = (RectTransform)viewportGO.transform;
            Stretch(_scrollViewport);
            viewportGO.AddComponent<RectMask2D>();
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // raycast target only

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _content = (RectTransform)contentGO.transform;
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = RowSpacing;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(0, 0, 4, 4);
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.viewport = _scrollViewport;
            _scrollRect.content = _content;
            return scrollGO;
        }

        GameObject BuildLoadingState(Transform parent)
        {
            var groupGO = new GameObject("LoadingState", typeof(RectTransform));
            groupGO.transform.SetParent(parent, false);
            Stretch((RectTransform)groupGO.transform);
            var layout = groupGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < 6; i++)
            {
                var skeleton = CreateImage("Skeleton" + i, groupGO.transform, ColorRow, _roundedSprite);
                var le = skeleton.AddComponent<LayoutElement>();
                le.preferredHeight = RowHeight;
                var img = skeleton.GetComponent<Image>();
                img.DOFade(0.45f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(i * 0.06f);
            }
            groupGO.SetActive(false);
            return groupGO;
        }

        GameObject BuildMessageState(Transform parent, string name, string title, string subtitle, System.Action onRetry)
        {
            var groupGO = new GameObject(name, typeof(RectTransform));
            groupGO.transform.SetParent(parent, false);
            Stretch((RectTransform)groupGO.transform);

            var blobGO = CreateImage("Blob", groupGO.transform, new Color(ColorOrange.r, ColorOrange.g, ColorOrange.b, 0.12f), _circleSprite);
            var blobRt = (RectTransform)blobGO.transform;
            blobRt.anchorMin = blobRt.anchorMax = new Vector2(0.5f, 0.58f);
            blobRt.sizeDelta = new Vector2(260, 260);
            blobRt.anchoredPosition = Vector2.zero;

            var titleGO = CreateText(name + "Title", groupGO.transform, title, 32, FontStyles.Bold, ColorTextPrimary, TextAlignmentOptions.Center);
            var titleRt = (RectTransform)titleGO.transform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.58f);
            titleRt.sizeDelta = new Vector2(560, 60);
            titleRt.anchoredPosition = new Vector2(0, -10);

            var subtitleGO = CreateText(name + "Subtitle", groupGO.transform, subtitle, 24, FontStyles.Normal, ColorTextSecondary, TextAlignmentOptions.Center);
            var subtitleRt = (RectTransform)subtitleGO.transform;
            subtitleRt.anchorMin = subtitleRt.anchorMax = new Vector2(0.5f, 0.58f);
            subtitleRt.sizeDelta = new Vector2(560, 80);
            subtitleRt.anchoredPosition = new Vector2(0, -56);

            if (onRetry != null)
            {
                var retryGO = CreateImage(name + "Retry", groupGO.transform, ColorOrange, _roundedSprite);
                var retryRt = (RectTransform)retryGO.transform;
                retryRt.anchorMin = retryRt.anchorMax = new Vector2(0.5f, 0.58f);
                retryRt.sizeDelta = new Vector2(220, 76);
                retryRt.anchoredPosition = new Vector2(0, -140);
                var retryButton = retryGO.AddComponent<Button>();
                retryButton.onClick.AddListener(() => onRetry());
                var retryLabel = CreateText("Label", retryGO.transform, "RETRY", 24, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
                Stretch((RectTransform)retryLabel.transform);
            }

            groupGO.SetActive(false);
            return groupGO;
        }

        // ---------------------------------------------------------------- Open / Close / Fetch

        public void Open(int yourBest)
        {
            gameObject.SetActive(true);
            Refresh(yourBest);
        }

        public void RequestClose()
        {
            if (UIManager.Instance != null) UIManager.Instance.ClosePopup(gameObject);
        }

        void RequestRetry()
        {
            Refresh(ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0);
        }

        public void Refresh(int yourBest)
        {
            int requestId = ++_pendingRequestId;
            ShowState(_loadingGroup);
            float startTime = Time.unscaledTime;

            LeaderboardService.FetchBoard(yourBest, (entries, success) =>
            {
                if (requestId != _pendingRequestId) return; // stale response, ignore

                float elapsed = Time.unscaledTime - startTime;
                float remaining = Mathf.Max(0f, MinLoadingSeconds - elapsed);
                DOVirtual.DelayedCall(remaining, () =>
                {
                    if (requestId != _pendingRequestId) return;
                    if (!success) { ShowState(_errorGroup); return; }
                    if (entries == null || entries.Count == 0) { ShowState(_emptyGroup); return; }
                    PopulateRows(entries);
                    ShowState(_listGroup);
                });
            });
        }

        void ShowState(GameObject active)
        {
            _loadingGroup.SetActive(active == _loadingGroup);
            _emptyGroup.SetActive(active == _emptyGroup);
            _errorGroup.SetActive(active == _errorGroup);
            _listGroup.SetActive(active == _listGroup);
        }

        void PopulateRows(List<Leaderboard.Entry> entries)
        {
            foreach (var row in _spawnedRows) Destroy(row);
            _spawnedRows.Clear();

            int youIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                int rank = i + 1;
                if (entry.You) youIndex = i;
                var row = BuildEntryRow(rank, entry);
                _spawnedRows.Add(row);

                // Stagger-in.
                var cg = row.AddComponent<CanvasGroup>();
                cg.alpha = 0;
                var rt = (RectTransform)row.transform;
                Vector2 target = rt.anchoredPosition;
                rt.anchoredPosition = target + new Vector2(0, 14);
                cg.DOFade(1, 0.25f).SetDelay(i * 0.035f).SetEase(Ease.OutQuad);
                rt.DOAnchorPos(target, 0.3f).SetDelay(i * 0.035f).SetEase(Ease.OutCubic);
            }

            Canvas.ForceUpdateCanvases();
            if (youIndex >= 0) ScrollToRow(youIndex, entries.Count);
        }

        void ScrollToRow(int index, int total)
        {
            if (_scrollRect == null || total <= 1) return;
            float rowStride = RowHeight + RowSpacing;
            float contentHeight = total * rowStride;
            float viewportHeight = _scrollViewport.rect.height;
            if (contentHeight <= viewportHeight) return;

            float rowCenterFromTop = index * rowStride + RowHeight * 0.5f;
            float targetTop = Mathf.Clamp(rowCenterFromTop - viewportHeight * 0.5f, 0, contentHeight - viewportHeight);
            float normalized = 1f - targetTop / (contentHeight - viewportHeight);
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        GameObject BuildEntryRow(int rank, Leaderboard.Entry entry)
        {
            bool isYou = entry.You;
            bool isTop3 = rank <= 3;
            Color rowColor = isYou ? ColorRowAlt : (rank % 2 == 0 ? ColorRowAlt : ColorRow);

            var rowGO = CreateImage(isYou ? "Row_YOU" : "Row_" + rank, null, rowColor, _roundedSprite);
            var le = rowGO.AddComponent<LayoutElement>();
            le.preferredHeight = RowHeight;

            if (isYou)
            {
                var outline = rowGO.AddComponent<Outline>();
                outline.effectColor = Color.Lerp(ColorBlue, ColorOrange, 0.5f);
                outline.effectDistance = new Vector2(2, -2);
            }

            var rankBadge = CreateImage("RankBadge", rowGO.transform, isTop3 ? RankColor(rank) : new Color(1, 1, 1, 0.06f), _circleSprite);
            var rankRt = (RectTransform)rankBadge.transform;
            rankRt.anchorMin = rankRt.anchorMax = new Vector2(0, 0.5f);
            rankRt.sizeDelta = new Vector2(56, 56);
            rankRt.anchoredPosition = new Vector2(46, 0);
            var rankLabel = CreateText("RankLabel", rankBadge.transform, rank.ToString(), 26, FontStyles.Bold,
                isTop3 ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : ColorTextPrimary, TextAlignmentOptions.Center);
            Stretch((RectTransform)rankLabel.transform);

            var nameGO = CreateText("Name", rowGO.transform, entry.Name, 26, isYou ? FontStyles.Bold : FontStyles.Normal,
                isYou ? Color.white : ColorTextPrimary, TextAlignmentOptions.Left);
            var nameRt = (RectTransform)nameGO.transform;
            nameRt.anchorMin = new Vector2(0, 0.5f);
            nameRt.anchorMax = new Vector2(1, 0.5f);
            nameRt.pivot = new Vector2(0, 0.5f);
            nameRt.anchoredPosition = new Vector2(94, 0);
            nameRt.sizeDelta = new Vector2(-330, 50);

            var scoreGO = CreateText("Score", rowGO.transform, entry.Score.ToString("N0"), 30, FontStyles.Bold,
                isYou ? ColorOrange : ColorTextPrimary, TextAlignmentOptions.Right);
            var scoreRt = (RectTransform)scoreGO.transform;
            scoreRt.anchorMin = new Vector2(1, 0.5f);
            scoreRt.anchorMax = new Vector2(1, 0.5f);
            scoreRt.pivot = new Vector2(1, 0.5f);
            scoreRt.anchoredPosition = new Vector2(-32, 0);
            scoreRt.sizeDelta = new Vector2(180, 50);
            var scoreTmp = scoreGO.GetComponent<TextMeshProUGUI>();
            scoreTmp.enableWordWrapping = false;

            rowGO.transform.SetParent(_content, false);
            return rowGO;
        }

        static Color RankColor(int rank)
        {
            switch (rank)
            {
                case 1: return ColorGold;
                case 2: return ColorSilver;
                default: return ColorBronze;
            }
        }

        // ---------------------------------------------------------------- Small UI helpers

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return go;
        }

        static GameObject CreateText(string name, Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            return go;
        }

        static void AddSubtleGradientWash(RectTransform card)
        {
            var top = CreateImage("BlueWash", card, new Color(ColorBlue.r, ColorBlue.g, ColorBlue.b, 0.10f), null);
            var topRt = (RectTransform)top.transform;
            topRt.anchorMin = new Vector2(0, 0.55f);
            topRt.anchorMax = new Vector2(1, 1);
            topRt.offsetMin = topRt.offsetMax = Vector2.zero;
            top.GetComponent<Image>().raycastTarget = false;
            top.AddComponent<LayoutElement>().ignoreLayout = true;

            var bottom = CreateImage("OrangeWash", card, new Color(ColorOrange.r, ColorOrange.g, ColorOrange.b, 0.08f), null);
            var bottomRt = (RectTransform)bottom.transform;
            bottomRt.anchorMin = new Vector2(0, 0f);
            bottomRt.anchorMax = new Vector2(1, 0.45f);
            bottomRt.offsetMin = bottomRt.offsetMax = Vector2.zero;
            bottom.GetComponent<Image>().raycastTarget = false;
            bottom.AddComponent<LayoutElement>().ignoreLayout = true;
        }
    }
}
