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
            RadialFormation playerFormation = other.GetComponentInParent<RadialFormation>();
            if (playerFormation == null || playerFormation.amount <= 0) return;

            AudioManager.Instance.PlayFightSound(AudioManager.Instance.cartoonFightSound );
            this.GetComponent<Collider>().enabled = false;
            GameFlowManager.Instance.UpdateGameState(GameState.MiniBattle);
            createdParticleSystem = Instantiate(bossFightParticle).gameObject;
            createdParticleSystem.transform.localScale = new Vector3(3, 3, 3);
            createdParticleSystem.transform.position = this.transform.position;
            StartCoroutine(DecreaseArmyOverTime(playerFormation));
        }

        private IEnumerator DecreaseArmyOverTime(RadialFormation playerFormation)
        {
            corridorEnemyCount = Mathf.Max(0, corridorEnemyCount);

            while (corridorEnemyCount > 0 && playerFormation.amount > 0)
            {
                corridorEnemyCount = Mathf.Max(0, corridorEnemyCount - 1);
                playerFormation.amount = Mathf.Max(0, playerFormation.amount - 1);
                GameFlowManager.Instance.SetPlayerCount(playerFormation.amount);
                UIManager.Instance.SetPlayerCountText(playerFormation.amount);

                yield return _getWait;
            }

            AudioManager.Instance.StopFightSound();
            GameFlowManager.Instance.UpdateGameState(
                playerFormation.amount > 0 ? GameState.Game : GameState.Lose);
            gameObject.SetActive(false);
            if (createdParticleSystem != null) Destroy(createdParticleSystem);
        }
    }
}
