using System;
using _Scripts.Core;
using DG.Tweening;
using UnityEngine;

public class Boss : MonoBehaviour
{

    private CameraController camController;
    private Transform cameraTransform;
    private Camera mainCamera;
    public Transform hitPoint;
    private float forceMultiplier;

    public CatBall catBall;

    private void Start()
    {
        mainCamera = Camera.main;
        camController = mainCamera.GetComponent<CameraController>();
        cameraTransform = mainCamera.GetComponent<Transform>();    
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Cat"))
        {
            GameFlowManager.Instance.UpdateGameState(GameState.Battle);
            this.GetComponent<Collider>().isTrigger = false;
            // Clamped: the eject impulse is force*10, tuned for crowds of ~20-50. An unclamped
            // 168-crowd launched the ball clean off the world — it never came to rest, the
            // velocity==zero win check never fired, and the game sat in Battle state forever
            // with the camera chasing the ball into the sky (caught by the 18:59 timeout
            // capture). The bonus strip maxes out at point100 anyway, so the clamp loses no
            // real reward range.
            forceMultiplier = Mathf.Min(other.transform.parent.GetComponent<FormationBase>().Amount, 60);
        }
    }


    public void EjectCatBallToLevelEnd()
    {
        catBall.KillTween();
        this.transform.GetChild(1).gameObject.SetActive(false);
        camController.lockCamera = false;
        camController.target = catBall.transform;
        cameraTransform.DORotate(new Vector3(40, 0, 0), 1.1F).OnComplete(
            () =>
            {
                AudioManager.Instance.PlayOneShot(AudioManager.Instance.catBallRollSound);
                mainCamera.GetComponent<CameraController>().offset = new Vector3(0, 11, -5);
                catBall.EjectCatBall(forceMultiplier,hitPoint);
            });
            
        //TODO : add pfx timers here.
    }
    
}
