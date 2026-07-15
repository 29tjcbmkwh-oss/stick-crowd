using System;
using _Scripts.Models;
using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Controllers
{
    public class ArmyMovementController : MonoBehaviour
    {
        public bool isGamePaused;
        private float _lastFrameFingerPositionX;
        private Vector3 _lastFrameFingerPosition;
        private float _moveFactorX;
        private float MoveFactorX => _moveFactorX;
        private Vector3 movementVector;


        void Update()
        {
            if (isGamePaused != false) return;

            ControlCharacter();
            //CLAMP X POS
            var pos = transform.position;
            pos.x =  Mathf.Clamp(transform.position.x, -Constants.CLAMP_MODIFIER, Constants.CLAMP_MODIFIER);
            transform.position = pos;
            
            Move();
        }
        
        private void ControlCharacter()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _lastFrameFingerPositionX = Input.mousePosition.x;
                _lastFrameFingerPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButton(0))
            {
                _moveFactorX = Input.mousePosition.x - _lastFrameFingerPositionX;
                _lastFrameFingerPositionX = Input.mousePosition.x;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _moveFactorX = 0f;
                RotateAnimatedRig(0);
            }
        }

        private void Move()
        {
            float swerveAmount = Constants.SWERVE_SPEED * MoveFactorX  * Time.fixedDeltaTime / Screen.width;
             // movementVector.x = Mathf.SmoothDamp(0f,swerveAmount, ref velocity, 1);
             movementVector.x =  Mathf.Lerp(0F,swerveAmount,  Constants.LERP_COEF);
            transform.Translate( (transform.forward  * Time.deltaTime * Constants.SPEED_COEFFICIENT ) + movementVector);

            // RotateAnimatedRig(MoveFactorX * 10F);
            var directionAmount = Mathf.Lerp(CalculateDirection().x , 0F, Time.deltaTime);
             RotateAnimatedRig( directionAmount );
        }

        private Vector3 CalculateDirection()
        {
            Vector3 temp = ( Input.mousePosition - _lastFrameFingerPosition);
            temp.y = 0;
            temp.z = 0;
            return temp;
        }

        
        private void RotateAnimatedRig(float dir)
        {
            for (int i = 1; i < this.transform.childCount; i++)
            {
                Cat cat = transform.GetChild(i).GetComponent<Cat>();
                cat.rotationTarget.transform.rotation = Quaternion.Euler(new Vector3(0, Mathf.Clamp(dir,-100F,100F),0));

            }
        }


    }
}
