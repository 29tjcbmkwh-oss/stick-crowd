using System;
using System.Collections;
using _Scripts.Core;
using UnityEngine;

namespace _Scripts.Models
{
    public class Cat : MonoBehaviour
    {
        private Animator _catAnimator;
        public Transform rotationTarget;

        private void Awake()
        {
            // InChildren: the stickman rig (with its Animator) is a child of the crowd-unit root.
            // Also finds an Animator on the root itself, so old prefabs keep working.
            _catAnimator = GetComponentInChildren<Animator>();
            SkinSystem.ApplyTo(gameObject); // equipped store skin (no-op for the default)
            RandomizeIdle();
            // Null-guard: pooled spawns can Awake during play-exit teardown after the
            // GameFlowManager singleton is already destroyed (exit-time NRE spam, 11:42 log).
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.state == GameState.Start)
            {
                ControlAnimationState(0);
            }
        }

        public void ControlAnimationState(int state)
        {
            if (state == 0)
            {
                _catAnimator.SetTrigger("stopRunning");
            }

            if (state == 1)
            {
                _catAnimator.SetTrigger("startRunning");
            }
        }
        
        private void RandomizeIdle()
        {
            _catAnimator.enabled = false;
            float waitToAnimate = UnityEngine.Random.Range(0, 0.2F);
            StartCoroutine(WaitForAnimate(waitToAnimate));
        }
        IEnumerator WaitForAnimate(float second)
        {
            yield return new WaitForSeconds(second);
            _catAnimator.enabled = true;
        }
    }
}
