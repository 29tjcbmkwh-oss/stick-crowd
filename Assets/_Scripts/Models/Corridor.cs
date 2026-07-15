using System.Collections;
using _Scripts.Controllers;
using _Scripts.Core;
using _Scripts.Utilities;
using DG.Tweening;
using UnityEngine;

namespace _Scripts.Models
{
    public class Corridor : MonoBehaviour
    {
        public int increaseAmount;
        public int decreaseAmount;
        public int multiplyAmount;
        public int divideAmount;

        public Constants.CorridorTypes _corridorType;
        [HideInInspector] public MeshRenderer meshRenderer;
        public BoxCollider neighbourCorridorCollider;

        private void Awake()
        {
            meshRenderer = this.GetComponent<MeshRenderer>();
        }
        
        
        public Constants.CorridorTypes GetCorridorType()
        {
            return _corridorType;
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Cat")) return;

            CorridorController.Instance.CorridorEffect(this, other.gameObject);

            // Disable Corridors
            neighbourCorridorCollider.enabled = false;
            this.GetComponent<BoxCollider>().enabled = false;
            
        }

    }
    
}
