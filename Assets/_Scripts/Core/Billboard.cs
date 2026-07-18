using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Keeps an object facing the camera each frame. Used for gate numbers so they are always
    /// upright and readable (hand-set world rotations rendered them mirrored/garbled).
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }
            // face the same direction the camera looks, so text is never mirrored
            transform.rotation = Quaternion.LookRotation(_cam.forward, _cam.up);
        }
    }
}
