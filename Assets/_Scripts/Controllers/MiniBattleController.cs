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
            AudioManager.Instance.PlayFightSound(AudioManager.Instance.cartoonFightSound );
            this.GetComponent<Collider>().enabled = false;
            GameFlowManager.Instance.UpdateGameState(GameState.MiniBattle);
            createdParticleSystem = Instantiate(bossFightParticle).gameObject;
            createdParticleSystem.transform.localScale = new Vector3(3, 3, 3);
            createdParticleSystem.transform.position = this.transform.position;
            StartCoroutine(DecreaseArmyOverTime(other.transform.parent.GetComponent<RadialFormation>()));
        }

        private IEnumerator DecreaseArmyOverTime(RadialFormation otherRadialFormation)
        {
            while (otherRadialFormation.amount != 0 && otherRadialFormation.amount  != 0) 
            {
                corridorEnemyCount -= 1;
                otherRadialFormation.amount  -= 1;
                print("enemy count is "  + corridorEnemyCount +"and player amount is " + otherRadialFormation.amount );

                yield return _getWait;
                
                if (corridorEnemyCount == 0)
                {
                    AudioManager.Instance.StopFightSound();
                    GameFlowManager.Instance.UpdateGameState(GameState.Game);
                    this.gameObject.SetActive(false);
                    Destroy(createdParticleSystem);
                    break;
                }  
                if (otherRadialFormation.amount == 0)
                {
                    AudioManager.Instance.StopFightSound();
                    GameFlowManager.Instance.UpdateGameState(GameState.Lose);
                    this.gameObject.SetActive(false);
                    Destroy(createdParticleSystem);
                    break;
                }
            }
        }
    }
}
