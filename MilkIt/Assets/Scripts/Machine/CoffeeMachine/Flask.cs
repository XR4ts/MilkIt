using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using MilkIt;
using MilkIt.Fluids;
using System.Collections.Generic;

namespace MilkIt.Machine.CoffeeMachine{
    public class Flask : MonoBehaviour
    {
        [SerializeField] public FluidSource fluidSource;
        [SerializeField] public float snapDelay = 2f;
        [SerializeField] public List<Collider> basketColliders;
    
        private FlaskDetector currentReceiver;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

        private float snapCooldownUntil = 0f;
        public bool CanSnap =>
            Time.time >= snapCooldownUntil;
        public void StartSnapCooldown(float duration)
        {
            snapCooldownUntil = Time.time + duration;
        }
    
        private void Awake()
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }
    
        private void OnEnable()
        {
            if (grabInteractable == null)
                grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    
            if (grabInteractable != null)
                grabInteractable.selectEntered.AddListener(OnGrabbed);
        }
    
        private void OnDisable()
        {
            if (grabInteractable != null)
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
    
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (currentReceiver != null){
                StartSnapCooldown(snapDelay);
                currentReceiver.RemoveFlask();
            }
        }
    
        public void SetReceiver(FlaskDetector receiver)
        {
            currentReceiver = receiver;
        }

        public void DisableBasketColliders(bool disable)
        {
            Debug.Log("Disabling colliders");
            for (int i =0; i<basketColliders.Count; i++)
            {
                Debug.Log($"Disabling collider: {i}");
                basketColliders[i].enabled = !disable;
            }
        }
    }
}