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
        // Pulse around the label's authored scale, never a hardcoded Vector3.one: gate labels
        // are authored at 0.16 to sit flush on the panel, and the old reset-to-one here was
        // silently blowing them up 6x the moment Play Mode enabled them — the editor-time
        // styling looked correct and the runtime frame didn't.
        private Vector3 _baseScale;
        private bool _baseCaptured;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _baseCaptured = true;
        }

        private void OnEnable()
        {
            if (!_baseCaptured) { _baseScale = transform.localScale; _baseCaptured = true; }
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.DOScale(_baseScale * 1.08f, 0.9f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDisable()
        {
            transform.DOKill();
        }
    }
}
