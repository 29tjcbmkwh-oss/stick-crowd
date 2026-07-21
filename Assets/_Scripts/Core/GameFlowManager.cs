using System;
using _Scripts.Ads;
using _Scripts.Controllers;
using _Scripts.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Core
{
    public class GameFlowManager : Singleton<GameFlowManager>
    {
        private FormationBase _radialFormation;
        [SerializeField] private GameObject player;
        [HideInInspector] public int playerCount;
        public BattleController battleController;
        
        public GameState state;
        private LevelController levelController; 
        private bool _lossHandled;
        private bool _winHandled;
        private bool _reviveUsed;
        private bool _doubleRewardUsed;
        private static bool s_reviveConsumedForAttempt;
        private static int s_rewardedRetryArmySize;
        public static event Action<GameState> onGameStateChange;

        public bool CanRevive => _lossHandled && !_reviveUsed;
        public bool CanDoubleReward => _winHandled && !_doubleRewardUsed &&
                                       ScoreManager.Instance != null && ScoreManager.Instance.CanDoubleLastReward;
        
        private void Start()
        {
            levelController = this.GetComponent<LevelController>();
            _radialFormation = player.GetComponent<FormationBase>();
            _reviveUsed = s_reviveConsumedForAttempt;
            if (s_rewardedRetryArmySize > 0)
            {
                _radialFormation.Amount = s_rewardedRetryArmySize;
                s_rewardedRetryArmySize = 0;
            }
            playerCount = _radialFormation.Amount;
            if (UIManager.Instance != null)
                UIManager.Instance.SetPlayerCountText(playerCount);
            UpdateGameState(GameState.Start);
            AudioManager.Instance.PlayTheme(AudioManager.Instance.mainThemeGame);
        }

        private void GameOver()
        {
            if (_lossHandled) return;
            _lossHandled = true;
            PauseGameFlow();
            _radialFormation.Amount = 0;
            playerCount = 0;
            UIManager.Instance.playerCountText.text = "0";
            UIManager.Instance.ActivatePopup(UIManager.Instance.gameOverPanel);
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.gameOverSound);
            // Ads: shows an interstitial every Nth loss (see AdConfig). No-op until the AdMob
            // SDK + ADMOB_ENABLED define are added, so this is safe to ship as-is.
            AdManager.Instance.NotifyGameOver();
        }

        private void GameWin()
        {
            if (_winHandled) return;
            _winHandled = true;
            ResetReviveAttempt();
            PauseGameFlow();
            ScoreManager.Instance.EnsureRunReward();
            UIManager.Instance.ActivatePopup(UIManager.Instance.gameWinPanel);
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.gameWinSound);
        }

        public void RestartGame()
        {
            ResetReviveAttempt();
            levelController.RestartLevel();
        }

        public void LoadNewLevel()
        {
            ResetReviveAttempt();
            levelController.NextLevel();
        }

        public void TryRevive(Action<bool> completed = null)
        {
            if (!CanRevive)
            {
                completed?.Invoke(false);
                return;
            }

            AdManager.Instance.ShowRewarded(() =>
            {
                _reviveUsed = true;
                s_reviveConsumedForAttempt = true;
                s_rewardedRetryArmySize = 5;
                completed?.Invoke(true);
                // Restarting is safer and fairer than attempting to rebuild a
                // destroyed crowd inside the boss camera rig. The rewarded
                // retry begins with five runners and cannot be chained again.
                levelController.RestartLevel();
            }, () => completed?.Invoke(false));
        }

        private static void ResetReviveAttempt()
        {
            s_reviveConsumedForAttempt = false;
            s_rewardedRetryArmySize = 0;
        }

        public void TryDoubleWinReward(Action<bool> completed = null)
        {
            if (!CanDoubleReward)
            {
                completed?.Invoke(false);
                return;
            }

            AdManager.Instance.ShowRewarded(() =>
            {
                bool doubled = ScoreManager.Instance.DoubleLastReward();
                _doubleRewardUsed = doubled;
                completed?.Invoke(doubled);
            }, () => completed?.Invoke(false));
        }
        
        public void UpdateGameState(GameState newState)
        {
            state = newState;

            switch (newState)
            {
                case GameState.Start: 
                    GameStart();
                    break;
                case GameState.Tutorial:
                    break;
                case GameState.Game:
                    ContinueGameFlow();
                    break; 
                case GameState.Battle:
                    BattlePhase();
                    break;
                case GameState.MiniBattle:
                    MiniBattlePhase();
                    break;
                case GameState.Pause:
                    PauseGameFlow();
                    break;
                case GameState.Win:
                    GameWin();
                    break;
                case GameState.Lose:
                    GameOver();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }
            
            // Notify game state changed;
            onGameStateChange?.Invoke(newState);
        }

        private void PauseGameFlow()
        {
            if (player == null) return;
            foreach (Cat cat in player.GetComponentsInChildren<Cat>(true))
                cat.ControlAnimationState(0);
            ArmyMovementController movement = player.GetComponent<ArmyMovementController>();
            if (movement != null) movement.isGamePaused = true;
        }

        private void ContinueGameFlow()
        {
            if (player == null) return;
            foreach (Cat cat in player.GetComponentsInChildren<Cat>(true))
                cat.ControlAnimationState(1);
            ArmyMovementController movement = player.GetComponent<ArmyMovementController>();
            if (movement != null) movement.isGamePaused = false;
        }

        private void GameStart()
        {
            PauseGameFlow();
        }
      
        public int GetPlayerCount(){
            return playerCount;
        }
        
        public void SetPlayerCount(int _score)
        {
            playerCount = _score;
        }

        private void BattlePhase()
        {
            AudioManager.Instance.PlayFightSound(AudioManager.Instance.cartoonFightSound);
            PauseGameFlow();
            battleController.Battle();
            UIManager.Instance.upperPanel.SetActive(false);
        }
        private void MiniBattlePhase()
        {
            PauseGameFlow();
        }

    }

    public enum GameState
    {
        Start,
        Tutorial,
        Game,
        MiniBattle,
        Battle,
        Pause,
        Win,
        Lose
    }
}
