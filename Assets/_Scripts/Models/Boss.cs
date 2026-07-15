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
            forceMultiplier = other.transform.parent.GetComponent<RadialFormation>().amount;
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
