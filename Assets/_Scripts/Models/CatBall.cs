using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CatBall : MonoBehaviour
{
    void Start()
    {
  AddMovementTween();
    }

    private void AddMovementTween()
    {
        transform.DOMoveY(transform.position.y + 0.5F, 1).SetLoops(-1, LoopType.Yoyo).SetId(2);
    }

    public void KillTween()
    {
        DOTween.Kill(2);
    }

    public void EjectCatBall(float forceMultiplier, Transform hitPoint)
    {
        this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePositionX;
        this.GetComponent<Rigidbody>().AddForceAtPosition(transform.forward.normalized * forceMultiplier * 10 ,
            hitPoint.position, ForceMode.Impulse);
    }
}
