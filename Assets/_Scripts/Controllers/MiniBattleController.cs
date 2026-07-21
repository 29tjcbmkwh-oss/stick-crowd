using System;
using System.Collections;
using _Scripts.Core;
using UnityEngine;

namespace _Scripts.Controllers
{
    public class MiniBattleController : MonoBehaviour
    {
        public int corridorEnemyCount ;
        private WaitForSeconds _getWait;
        public ParticleSystem bossFightParticle;
        private GameObject createdParticleSystem;
        
        private void Start()
        {
            _getWait = new WaitForSeconds(0.2F);
        }

        private void OnTriggerEnter(Collider other)
        {
            FormationBase playerFormation = other.GetComponentInParent<FormationBase>();
            if (playerFormation == null || playerFormation.Amount <= 0) return;

            AudioManager.Instance.PlayFightSound(AudioManager.Instance.cartoonFightSound );
            this.GetComponent<Collider>().enabled = false;
            GameFlowManager.Instance.UpdateGameState(GameState.MiniBattle);
            createdParticleSystem = Instantiate(bossFightParticle).gameObject;
            createdParticleSystem.transform.localScale = new Vector3(3, 3, 3);
            createdParticleSystem.transform.position = this.transform.position;
            StartCoroutine(DecreaseArmyOverTime(playerFormation));
        }

        private IEnumerator DecreaseArmyOverTime(FormationBase playerFormation)
        {
            corridorEnemyCount = Mathf.Max(0, corridorEnemyCount);

            while (corridorEnemyCount > 0 && playerFormation.Amount > 0)
            {
                corridorEnemyCount = Mathf.Max(0, corridorEnemyCount - 1);
                playerFormation.Amount = Mathf.Max(0, playerFormation.Amount - 1);
                GameFlowManager.Instance.SetPlayerCount(playerFormation.Amount);
                UIManager.Instance.SetPlayerCountText(playerFormation.Amount);

                yield return _getWait;
            }

            AudioManager.Instance.StopFightSound();
            GameFlowManager.Instance.UpdateGameState(
                playerFormation.Amount > 0 ? GameState.Game : GameState.Lose);
            gameObject.SetActive(false);
            if (createdParticleSystem != null) Destroy(createdParticleSystem);
        }
    }
}
