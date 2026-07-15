using System;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Controllers;
using _Scripts.Core;
using DG.Tweening;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public int decreaseAmount;
    public ParticleSystem catDeadFpx;
    private GameObject fpxGo;
    private void Start()
    {
        if (this.transform.name == "Obstacle_IronBar_01")
        {
            transform.DORotate(new Vector3(0, 0, 1.3F), 1).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutQuad);
        }
        if (this.transform.name == "Obstacle_AirConditioner_01")
        {
            this.transform.GetChild(1).DORotate(new Vector3(0,0,360),3 , RotateMode.FastBeyond360 )
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Cat")) return;
        
        other.transform.parent.GetComponent<RadialFormation>().amount -= decreaseAmount;
        fpxGo = Instantiate(catDeadFpx).gameObject;
        fpxGo.transform.position = new Vector3(other.transform.position.x, other.transform.position.y + 0.35F , other.transform.position.z);
        
        other.transform.parent.GetComponent<ArmyController>().KillGameObject(other.gameObject);
        // Update UI
        // UIManager.Instance.SetPlayerCountText(other.transform.parent.GetComponent<RadialFormation>().amount);
        // Game Over check
        if (other.transform.parent.GetComponent<RadialFormation>().amount <= 0)
        {
            GameFlowManager.Instance.UpdateGameState(GameState.Lose);
        }
        
        AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);

    }

}
