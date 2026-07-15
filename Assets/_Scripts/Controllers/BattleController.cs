using System;
using System.Collections;
using _Scripts.Core;
using DG.Tweening;
using Lofelt.NiceVibrations;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Controllers
{
    public class BattleController : MonoBehaviour
    {
        private bool didLost = false;
        private bool didWon = false;
        public bool canStart = false;
        private bool canEject = false;

        private float playerBarAmount = 0.5f;
        private float enemyBarAmount = 1F;
       
        public Image enemyBar;
        public Image playerBar;
        public Image playerBar2;
        public RectTransform middleBar;
        
        public GameObject battleUI;
        private Boss bossComponent;
        
        public float increaseAmount;

        public Camera mainCamera;
        private CameraController camController;
        private Transform cameraTransform;
        
        private void Awake()
        {
            camController = mainCamera.GetComponent<CameraController>();
            cameraTransform = mainCamera.GetComponent<Transform>();
        }

        private void Start()
        {
            bossComponent = FindObjectOfType<Boss>();
        }

        public void Battle()
        {
            cameraTransform.DOMove(new Vector3(9F, 9F, 100F), 1).OnComplete(OpenBattleScene);
            cameraTransform.DORotate(new Vector3(25, -50, 0), 0.5F);
        }

        private void Update()
        {
            if (canEject)
            {
                if (bossComponent.catBall.GetComponent<Rigidbody>().velocity == Vector3.zero)
                {
                    ScoreManager.Instance.EndGamePopupScore(bossComponent.gameObject);
                    GameFlowManager.Instance.UpdateGameState(GameState.Win);
                    canEject = false;
                }
            }
            
            if (!canStart) return;
            if (didLost) return;
            if (didWon) return;

            playerBarAmount -= Time.deltaTime * increaseAmount;
            enemyBarAmount += Time.deltaTime * increaseAmount * 2F;

            middleBar.anchoredPosition = new Vector2((playerBar2.fillAmount - 0.5F) * 820F, middleBar.anchoredPosition.y); 
            
            playerBar.fillAmount = playerBarAmount * 2F;
            playerBar2.fillAmount = playerBarAmount;
            enemyBar.fillAmount = enemyBarAmount;
            
            if (playerBarAmount  <= 0)
            {
                didLost = true;
                print("PLAYER LOST THE BATTLE");
                UIManager.Instance.ClosePopup(battleUI);
                GameFlowManager.Instance.UpdateGameState(GameState.Lose);
            }
            if (playerBarAmount >= 1)
            {
                CloseBattleScene();
            }
            
        }
        
        public void Decrease()
        {
            playerBarAmount += increaseAmount;
            enemyBarAmount -= increaseAmount * 2F;
            middleBar.anchoredPosition = new Vector2((playerBar2.fillAmount - 0.5F) * 820F, middleBar.anchoredPosition.y); 
            
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
        }

        private void OpenBattleScene()
        {

            camController.lockCamera = true;
            canStart = true;
            UIManager.Instance.ActivatePopup(battleUI);
            print("OPEN BATTLE SCENE");
        }

        private void CloseBattleScene()
        {
            AudioManager.Instance.StopFightSound();
            didWon = true;
            canStart = false;
            UIManager.Instance.ClosePopup(battleUI);
             bossComponent.EjectCatBallToLevelEnd();
             StartCoroutine(UpdateCanEjectBool());

        }
        private IEnumerator UpdateCanEjectBool()
        {
            yield return new WaitForSeconds(2);
            canEject = true;
        }
    }
}
