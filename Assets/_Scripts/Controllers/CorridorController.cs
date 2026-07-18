using System;
using _Scripts.Core;
using _Scripts.Models;
using _Scripts.Utilities;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace _Scripts.Controllers
{
    public class CorridorController : Singleton<CorridorController>
    {
        public int score  = 0 ;
        public void CorridorEffect(Corridor corridor, GameObject other)
        {
            RadialFormation formation = other.GetComponentInParent<RadialFormation>();
            if (formation == null) return;

            switch (corridor.GetCorridorType())
            {
                case Constants.CorridorTypes.Increase:
                    formation.amount += corridor.increaseAmount;
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorIncreaseSound);
                    print("INCREASING ARMY AMOUNT");
                    break; 
                case Constants.CorridorTypes.Decrease:
                    formation.amount -= corridor.decreaseAmount;
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);

                    print("DECREASE ARMY AMOUNT");
                    break;
                case Constants.CorridorTypes.Multiply:
                    formation.amount *= Mathf.Max(1, corridor.multiplyAmount);
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorIncreaseSound);
                    print("Multiply ARMY AMOUNT");
                    break;
                case Constants.CorridorTypes.Divide:
                    formation.amount /= Mathf.Max(1, corridor.divideAmount);
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);
                    print("Divide ARMY AMOUNT");
                    break;
                default:
                    Debug.Log("TRIGGER EXCEPTION");
                    break;
            }

            formation.amount = Mathf.Max(0, formation.amount);

            GameJuice.OnGatePassed(corridor,
                corridor.GetCorridorType() is Constants.CorridorTypes.Increase
                                            or Constants.CorridorTypes.Multiply);

            HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);

            score += formation.amount;
            ScoreManager.Instance.upperScoreText.text = score.ToString();
            
            GameFlowManager.Instance.SetPlayerCount(formation.amount);
            // Update UI
            UIManager.Instance.SetPlayerCountText(formation.amount);
            // Game Over check
            if (formation.amount <= 0)
            {
                GameFlowManager.Instance.UpdateGameState(GameState.Lose);
            }

        }

        
    }
}
