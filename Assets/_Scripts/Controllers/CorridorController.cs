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
            FormationBase formation = other.GetComponentInParent<FormationBase>();
            if (formation == null) return;

            switch (corridor.GetCorridorType())
            {
                case Constants.CorridorTypes.Increase:
                    formation.Amount += corridor.increaseAmount;
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorIncreaseSound);
                    break;
                case Constants.CorridorTypes.Decrease:
                    formation.Amount -= corridor.decreaseAmount;
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);
                    break;
                case Constants.CorridorTypes.Multiply:
                    formation.Amount *= Mathf.Max(1, corridor.multiplyAmount);
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorIncreaseSound);
                    break;
                case Constants.CorridorTypes.Divide:
                    formation.Amount /= Mathf.Max(1, corridor.divideAmount);
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance.doorMinusSound);
                    break;
                default:
                    Debug.LogWarning("[CorridorController] Unhandled corridor type: " + corridor.GetCorridorType());
                    break;
            }

            formation.Amount = Mathf.Max(0, formation.Amount);

            GameJuice.OnGatePassed(corridor,
                corridor.GetCorridorType() is Constants.CorridorTypes.Increase
                                            or Constants.CorridorTypes.Multiply);

            HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);

            score += formation.Amount;
            ScoreManager.Instance.upperScoreText.text = score.ToString();

            GameFlowManager.Instance.SetPlayerCount(formation.Amount);
            // Update UI
            UIManager.Instance.SetPlayerCountText(formation.Amount);
            // Same label text the gate itself displays (ThemeSetup.GateLabelText is
            // editor-only, so mirrored inline here).
            string gateLabel = corridor.GetCorridorType() switch
            {
                Constants.CorridorTypes.Increase => $"+{corridor.increaseAmount}",
                Constants.CorridorTypes.Decrease => $"-{corridor.decreaseAmount}",
                Constants.CorridorTypes.Multiply => $"x{corridor.multiplyAmount}",
                Constants.CorridorTypes.Divide => $"/{corridor.divideAmount}",
                _ => "?",
            };
            _Scripts.Analytics.Analytics.GatePass(
                corridor.GetCorridorType().ToString(), gateLabel, formation.Amount);
            // Game Over check
            if (formation.Amount <= 0)
            {
                _Scripts.Analytics.Analytics.PendingLossCause = "gate_decrease";
                GameFlowManager.Instance.UpdateGameState(GameState.Lose);
            }

        }

        
    }
}
