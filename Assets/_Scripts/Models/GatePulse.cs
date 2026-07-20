using DG.Tweening;
using UnityEngine;

namespace _Scripts.Models
{
    /// <summary>
    /// Subtle idle "breathing" scale loop on a gate label so gates read as alive/interactive
    /// from a distance, not just static plates. Attached automatically by VisualOverhaul.cs.
    /// </summary>
    public class GatePulse : MonoBehaviour
    {
        private void OnEnable()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOScale(1.08f, 0.9f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDisable()
        {
            transform.DOKill();
        }
    }
}
