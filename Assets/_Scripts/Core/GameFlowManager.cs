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
        // The level's serialized starting crowd (captured in Start BEFORE any rewarded-retry
        // override), so the revive bonus below scales with whatever the designer sets instead
        // of hardcoding a count that can silently fall behind it.
        private int _defaultArmySize;
        public static event Action<GameState> onGameStateChange;

        public bool CanRevive => _lossHandled && !_reviveUsed;
        public bool CanDoubleReward => _winHandled && !_doubleRewardUsed &&
                                       ScoreManager.Instance != null && ScoreManager.Instance.CanDoubleLastReward;
        
        private void Start()
        {
            levelController = this.GetComponent<LevelController>();
            _radialFormation = player.GetComponent<FormationBase>();
            _reviveUsed = s_reviveConsumedForAttempt;
            _defaultArmySize = _radialFormation.Amount; // before the retry override below
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
            // Cause was set by whichever system pushed the Lose state (obstacle / gate / boss).
            _Scripts.Analytics.Analytics.LevelFail(PlayerPrefs.GetInt("level", 0) + 1);
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
            _Scripts.Analytics.Analytics.LevelComplete(PlayerPrefs.GetInt("level", 0) + 1);
            ResetReviveAttempt();
            PauseGameFlow();
            ScoreManager.Instance.EnsureRunReward();
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.gameWinSound);
            // Reward-choice boxes first (Group B), win panel after the player continues.
            var canvas = UIManager.Instance.startButton != null ? UIManager.Instance.startButton.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
                RewardChoicePanel.Show(canvas.transform,
                    () => UIManager.Instance.ActivatePopup(UIManager.Instance.gameWinPanel));
            else
                UIManager.Instance.ActivatePopup(UIManager.Instance.gameWinPanel);
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
                // DOUBLE the normal starting crowd, never a fixed count: the old hardcoded 5
                // was WORSE than declining the ad and restarting free at the level default
                // (22) — a backwards incentive found by Marketing Lead 2026-07-23. Derived
                // from the captured default so it can never fall behind a redesigned level.
                s_rewardedRetryArmySize = Mathf.Max(_defaultArmySize * 2, _defaultArmySize + 1);
                completed?.Invoke(true);
                // Restarting is safer and fairer than attempting to rebuild a
                // destroyed crowd inside the boss camera rig (a true in-place revive would
                // resurrect a near-zero crowd anyway — you lose BECAUSE the crowd hit zero,
                // so "preserve size at death" preserves nothing worth having). The rewarded
                // retry restarts with a 2x head start and cannot be chained again.
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
            // Level-start fires only on the real run start (Start -> Game), not on resume
            // from Pause or on the post-MiniBattle return to Game.
            if (newState == GameState.Game && state == GameState.Start)
                _Scripts.Analytics.Analytics.LevelStart(PlayerPrefs.GetInt("level", 0) + 1);
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
