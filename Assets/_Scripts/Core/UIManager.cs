using System;
using Crystal;
using DG.Tweening;
using Lofelt.NiceVibrations;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.Core
{
    public class UIManager : Singleton<UIManager>
    {
        public TextMeshProUGUI playerCountText;
        public GameObject startButton;
        public GameObject gameOverPanel;
        public GameObject gameWinPanel;
        public GameObject upperPanel;
        public GameObject startButtonBack;
        public GameObject allPanelsBack;

        public Button soundButton;
        public Button musicButton;
        public Button vibrationButton;
        
        public TextMeshProUGUI levelText;
    
        public Sprite toggleOff;
        public Sprite toggleOn;
        public Image progressBar;
        public GameObject levelCountUIObject;
        public GameObject progressBarUIObject;

        private LeaderboardPanel leaderboardPanel;

        private void Start()
        {
            GameFlowManager.onGameStateChange += GameFlowManagerOnGameStateChange;
            levelText.text = "LEVEL " + (PlayerPrefs.GetInt("level", 0) + 1);
            PolishHud();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                leaderboardPanel = LeaderboardPanel.Build(canvas.transform);
                BuildLeaderboardButton(canvas);
            }
        }

        // Restyles the always-visible score/level/progress HUD to the Blue vs Orange
        // Runner brand tokens instead of the template's default TMP/Image styling. Done at
        // runtime against the existing serialized references (not by hand-editing
        // Canvas.prefab's YAML) — same safe pattern already used by LeaderboardPanel/
        // ProgressionUI in this project.
        private void PolishHud()
        {
            StyleHudLabel(levelText, 0.85f);
            StyleCrowdCounter(playerCountText);
            if (ScoreManager.Instance != null) StyleHudLabel(ScoreManager.Instance.upperScoreText, 1f);

            if (progressBar != null)
            {
                progressBar.color = _Scripts.Utilities.BrandPalette.Blue;
                Image track = progressBar.transform.parent != null
                    ? progressBar.transform.parent.GetComponent<Image>()
                    : null;
                if (track != null && track != progressBar)
                    track.color = new Color(0f, 0f, 0f, 0.35f);
            }
        }

        // The crowd counter sits on the brand-blue speech-bubble chip (added by
        // VisualOverhaul.AddCounterBubble per HOD A-list A4) — white bold number on blue, like
        // the genre reference. The previous brand-blue fill predates the chip and rendered
        // blue-on-blue (invisible) once the chip existed.
        private static void StyleCrowdCounter(TMP_Text label)
        {
            if (label == null) return;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            if (label.fontSharedMaterial != null)
            {
                label.outlineWidth = 0.12f;
                label.outlineColor = new Color32(15, 20, 32, 200); // subtle dark edge for thumbnail pop
            }
            label.characterSpacing = 1f;
        }

        private static void StyleHudLabel(TMP_Text label, float sizeMultiplier)
        {
            if (label == null) return;
            label.fontStyle = FontStyles.Bold;
            label.color = _Scripts.Utilities.BrandPalette.TextPrimary;
            label.fontSize *= sizeMultiplier;
            // Setting outlineWidth forces TMP to build an outline material instance from the
            // label's current font material — throws ArgumentNullException if this TMP_Text
            // has no font asset/material assigned (confirmed crash on levelText/playerCountText
            // 2026-07-20: their template-authored TMP components never had one wired). Guard
            // instead of assuming every TMP_Text in this scene is fully configured.
            if (label.fontSharedMaterial != null)
            {
                label.outlineWidth = 0.2f;
                label.outlineColor = new Color32(6, 8, 14, 200);
            }
            label.characterSpacing = 1f;
        }

        private void OnDestroy()
        {
            GameFlowManager.onGameStateChange -= GameFlowManagerOnGameStateChange;
        }
        
        private void GameFlowManagerOnGameStateChange(GameState obj)
        {
            startButton.SetActive(obj == GameState.Start);

            startButtonBack.SetActive(obj == GameState.Start);
            
            startButton.transform.DOMoveY(320, 1F);
            
            if (obj == GameState.Lose)
            {
                ActivatePopup(gameOverPanel);
            }

            ChangeUpperPanelElements(obj);
        }

        public void UpdateProcess(float proc)
        {
            progressBar.fillAmount = proc;
        }
        
        public string SetPlayerCountText(int amount)
        {
            string text = playerCountText.text = amount.ToString();
            // Crowd counter is the number the player stares at most (Visual Reskin Spec §5) —
            // it must pop on every change, not just silently update.
            playerCountText.transform.DOKill();
            playerCountText.transform.localScale = Vector3.one;
            playerCountText.transform.DOPunchScale(Vector3.one * 0.35f, 0.3f, 6, 0.6f);
            return text;
        }
        
        public void ActivatePopup(GameObject popupType)
        {
            upperPanel.SetActive(false);
            allPanelsBack.SetActive(true);
            startButton.SetActive(false);
            if (GameFlowManager.Instance.state == GameState.Game)
            {
                GameFlowManager.Instance.UpdateGameState(GameState.Pause);
            }
            popupType.SetActive(true);
            // OutBack (a slight overshoot pop) reads far better for a modal entering than the
            // previous InExpo, which eases the wrong direction for something appearing on screen.
            popupType.transform.DOScale(Vector3.one, 0.4F).SetEase(Ease.OutBack);
            PolishPopupLabels(popupType);

            AudioManager.Instance.PlayOneShot(AudioManager.Instance.clickSound);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
        }

        // Applies the same brand-consistent text styling used on the HUD (see PolishHud) to
        // whatever labels the game-over/win/leaderboard panel already contains, without
        // touching Canvas.prefab directly. Deliberately does NOT touch fontSize (unlike
        // StyleHudLabel) — ActivatePopup can run repeatedly on the same panel within one
        // scene lifetime (e.g. reopening the leaderboard), and a multiplicative size bump
        // would compound on every open. Color/weight/outline are safe to reapply as-is.
        private static void PolishPopupLabels(GameObject popup)
        {
            foreach (TMP_Text label in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                label.fontStyle = FontStyles.Bold;
                label.color = _Scripts.Utilities.BrandPalette.TextPrimary;
                if (label.fontSharedMaterial != null) // see StyleHudLabel for why this guard exists
                {
                    label.outlineWidth = 0.2f;
                    label.outlineColor = new Color32(6, 8, 14, 200);
                }
            }
        }

        public void ClosePopup(GameObject popupType)
        {
            upperPanel.SetActive(true);
            allPanelsBack.SetActive(false);
            
            startButton.SetActive(GameFlowManager.Instance.state == GameState.Start);

            if (GameFlowManager.Instance.state == GameState.Pause)
            {
                GameFlowManager.Instance.UpdateGameState(GameState.Game);
            }
            // InBack for exit: accelerates away quickly and feels responsive, mirroring the
            // OutBack pop-in in ActivatePopup rather than the previous mismatched OutExpo.
            popupType.transform.DOScale(Vector3.zero, 0.25F).SetEase(Ease.InBack).OnComplete(() => popupType.SetActive(false));
            
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.clickSound);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
        }
        
        public void CloseStartButton()
        {
            startButton.transform.GetComponent<Transform>().DOMoveY(-100, 0.3F)
                .OnComplete(() =>
                {
                    startButton.SetActive(false);
                    startButtonBack.SetActive(false);
                    StartButton();
                });
        }

        private void StartButton()
        {
            GameFlowManager.Instance.UpdateGameState(GameState.Game);
        }

        public void RestartButton()
        {
            GameFlowManager.Instance.RestartGame();
        }

        public void NextLevelButton()
        {
            GameFlowManager.Instance.LoadNewLevel();
        }

        public void OpenLeaderboard()
        {
            if (leaderboardPanel == null) return;
            ActivatePopup(leaderboardPanel.gameObject);
            leaderboardPanel.Open(ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0);
        }

        // Small always-reachable corner entry point. Built at runtime as a direct child of the
        // Canvas (not nested inside upperPanel/gameOverPanel) so it can never overlap or be
        // hidden behind the existing HUD/popup layouts.
        private void BuildLeaderboardButton(Canvas canvas)
        {
            var safeAreaGO = new GameObject("LeaderboardButtonSafeArea", typeof(RectTransform));
            safeAreaGO.transform.SetParent(canvas.transform, false);
            var safeRt = (RectTransform)safeAreaGO.transform;
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeAreaGO.AddComponent<SafeArea>();

            var buttonGO = new GameObject("LeaderboardButton", typeof(RectTransform));
            buttonGO.transform.SetParent(safeAreaGO.transform, false);
            var rt = (RectTransform)buttonGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(132, 64);
            rt.anchoredPosition = new Vector2(-24, -24);

            var bg = buttonGO.AddComponent<Image>();
            bg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.08f, 0.16f, 0.82f);

            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;
            button.onClick.AddListener(OpenLeaderboard);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRt = (RectTransform)labelGO.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "RANK";
            label.fontSize = 24;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color32(0xFF, 0x7A, 0x2F, 0xFF);
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 2;
            label.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        public void HideOutcomePanels()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (gameWinPanel != null) gameWinPanel.SetActive(false);
            if (allPanelsBack != null) allPanelsBack.SetActive(false);
            if (upperPanel != null) upperPanel.SetActive(true);
        }
        
        bool currentMusicState;
        bool currentSoundState;
        bool currentVibrationState;
        
        public void SettingsToggle(Button button)
        {
            string target = button.transform.name;
            if (target == "VibrationButton")
            {
                currentVibrationState = AudioManager.Instance.ToggleVibration();
                ChangeToggleStatus(button, currentVibrationState);
                PlayerPrefs.SetInt("vibration", Convert.ToInt32(currentVibrationState));
            }
            if (target == "MusicButton")
            {
                currentMusicState = AudioManager.Instance.ToggleTheme();
                ChangeToggleStatus(button, currentMusicState);

                PlayerPrefs.SetInt("music", Convert.ToInt32(currentMusicState));
            }
            if (target == "SoundButton")
            {
                currentSoundState = AudioManager.Instance.ToggleSound();
                ChangeToggleStatus(button, currentSoundState);
                PlayerPrefs.SetInt("sound", Convert.ToInt32(currentSoundState));
            }
        }
        
        public void ChangeToggleStatus(Button button, bool currentState)
        {
            button.GetComponent<Image>().sprite = currentState ? toggleOn  : toggleOff ;
        }

        private void ChangeUpperPanelElements(GameState state)
        {
            if (state == GameState.Start)
            {
                levelCountUIObject.SetActive(true);
            }

            if (state == GameState.Game)
            {
                progressBarUIObject.SetActive(true);
                levelCountUIObject.SetActive(false);
            }
        }


    }
}
