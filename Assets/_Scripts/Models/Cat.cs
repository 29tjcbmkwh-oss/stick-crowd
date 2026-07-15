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
            _catAnimator = GetComponent<Animator>();
            RandomizeIdle();
            if (GameFlowManager.Instance.state == GameState.Start)
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
