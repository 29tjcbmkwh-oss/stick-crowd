using System;
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
        
        private void Start()
        {
            GameFlowManager.onGameStateChange += GameFlowManagerOnGameStateChange;
            levelText.text = "level " + (SceneManager.GetActiveScene().buildIndex );
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
            return playerCountText.text = amount.ToString();
        }
        
        public void ActivatePopup(GameObject popupType)
        {
            upperPanel.SetActive(false);
            allPanelsBack.SetActive(true);
            startButton.SetActive(false);
            if (GameFlowManager.Instance.state != GameState.Start && GameFlowManager.Instance.state != GameState.Battle )
            {
                GameFlowManager.Instance.UpdateGameState(GameState.Pause);
            }
            popupType.SetActive(true);
            popupType.transform.GetComponent<Transform>().DOScale(new Vector3(1, 1, 1), 0.4F).SetEase(Ease.InExpo);
            
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.clickSound);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
        }

        public void ClosePopup(GameObject popupType)
        {
            upperPanel.SetActive(true);
            allPanelsBack.SetActive(false);
            
            startButton.SetActive(GameFlowManager.Instance.state == GameState.Start);

            if (GameFlowManager.Instance.state != GameState.Start && GameFlowManager.Instance.state != GameState.Battle)
            {
                GameFlowManager.Instance.UpdateGameState(GameState.Game);
            }
            popupType.transform.GetComponent<Transform>().DOScale(new Vector3(0, 0, 0), 0.3F).SetEase(Ease.OutExpo).OnComplete(() => popupType.SetActive(false));
            
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
                print("****  " + currentMusicState);

                currentMusicState = AudioManager.Instance.ToggleTheme();
                print("**** 2 " + currentMusicState);
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
