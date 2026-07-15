using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ShineImage : MonoBehaviour
{
    void Start()
    {
        this.transform.DORotate(new Vector3(0,0,360),3 , RotateMode.FastBeyond360 )
            .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
    }
    
}
