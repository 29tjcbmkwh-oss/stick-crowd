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
        private RadialFormation _radialFormation;
        [SerializeField] private GameObject player;
        [HideInInspector] public int playerCount;
        public BattleController battleController;
        
        public GameState state;
        private LevelController levelController; 
        public static event Action<GameState> onGameStateChange;
        
        private void Start()
        {
            levelController = this.GetComponent<LevelController>();
            _radialFormation = player.GetComponent<RadialFormation>();
            playerCount = _radialFormation.amount;
            UpdateGameState(GameState.Start);
            AudioManager.Instance.PlayTheme(AudioManager.Instance.mainThemeGame);
        }

        private void GameOver()
        {
            PauseGameFlow();
            UIManager.Instance.playerCountText.text = "0";
            UIManager.Instance.ActivatePopup(UIManager.Instance.gameOverPanel);
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.gameOverSound);
            // Ads: shows an interstitial every Nth loss (see AdConfig). No-op until the AdMob
            // SDK + ADMOB_ENABLED define are added, so this is safe to ship as-is.
            AdManager.Instance.NotifyGameOver();
        }

        private void GameWin()
        {
            UIManager.Instance.ActivatePopup(UIManager.Instance.gameWinPanel);
            AudioManager.Instance.PlayOneShot(AudioManager.Instance.gameWinSound);
        }

        public void RestartGame()
        {
            print("Restart this level");
            levelController.RestartLevel();
        }

        public void LoadNewLevel()
        {
            levelController.NextLevel();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
                    print("// Battle phase logic here ");
                    BattlePhase();
                    break;
                case GameState.MiniBattle:
                    print("// Mini battle phase logic here ");
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
            for (int i = 1; i < player.transform.childCount ; i++)
            {
                player.transform.GetChild(i).GetComponent<Cat>().ControlAnimationState(0);
            }
            player.GetComponent<ArmyMovementController>().isGamePaused = true;
        }

        private void ContinueGameFlow()
        {
            for (int i = 1; i < player.transform.childCount ; i++)
            {
                player.transform.GetChild(i).GetComponent<Cat>().ControlAnimationState(1);
            }
            player.GetComponent<ArmyMovementController>().isGamePaused = false;
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
