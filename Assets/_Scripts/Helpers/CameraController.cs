
using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{ 
    public Vector3 offset;
    public Transform target;
    private Vector3 temp;
    public bool lockCamera;
    public RadialFormation formationArmy;
    void FixedUpdate()
    {
        if (!lockCamera)
        {
            if (target != null)
            {
                temp = target.position;
            }
            //temp.z = 0;
            temp.y = 0;
             // temp.x = 0;
             transform.position = Vector3.Lerp(transform.position, temp+ new Vector3(offset.x, 
                 offset.y + formationArmy.amount / 25, offset.z - formationArmy.amount / 25), 5*Time.deltaTime);
        }
    }
}
