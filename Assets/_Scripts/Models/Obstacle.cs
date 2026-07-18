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
        
        RadialFormation formation = other.GetComponentInParent<RadialFormation>();
        ArmyController army = other.GetComponentInParent<ArmyController>();
        if (formation == null || army == null) return;

        formation.amount = Mathf.Max(0, formation.amount - Mathf.Max(1, decreaseAmount));
        fpxGo = Instantiate(catDeadFpx).gameObject;
        fpxGo.transform.position = new Vector3(other.transform.position.x, other.transform.position.y + 0.35F , other.transform.position.z);
        
        army.KillGameObject(other.gameObject);
        GameFlowManager.Instance.SetPlayerCount(formation.amount);
        UIManager.Instance.SetPlayerCountText(formation.amount);
        // Update UI
        // UIManager.Instance.SetPlayerCountText(other.transform.parent.GetComponent<RadialFormation>().amount);
        // Game Over check
        if (formation.amount <= 0)
        {
            GameFlowManager.Instance.UpdateGameState(GameState.Lose);
        }
        
        AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);

    }

}
