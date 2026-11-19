using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using MilkIt;
using MilkIt.Fluids;
using System.Collections;
using System.Collections.Generic;

namespace MilkIt.Machine.CoffeeMachine{
    public class Flask : MonoBehaviour
    {
        [SerializeField] public FluidSource fluidSource;
        [SerializeField] public const float snapDelay = 2f;
        [SerializeField] public List<Collider> basketColliders;
        [SerializeField] public Vector3 baseScale;
        [SerializeField] public Transform defaultParent;
    
        private FlaskDetector currentReceiver;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        private Rigidbody rb;

        private float snapCooldownUntil = 0f;
        public bool CanSnap =>
            Time.time >= snapCooldownUntil;
        public void StartSnapCooldown(float duration = snapDelay)
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
                currentReceiver.RemoveFlask(this, args.interactorObject);
            }
        }
    
        public void SetReceiver(FlaskDetector receiver)
        {
            currentReceiver = receiver;
        }

        public void WhenConnected(bool disable)
        {
            if (disable)
            {
                SetBasketColliders(!disable);
            }
            else 
            {
                StartCoroutine(EnableBasketCollidersDelayed(!disable));
                
                if (rb == null)
                    rb = GetComponent<Rigidbody>();
                Debug.Log($"Changing rb to {disable}");
                rb.isKinematic = false;
                
                Debug.Log($"Changing parent to {defaultParent}");
                Transform tf = GetComponent<Transform>();
                tf.SetParent(defaultParent);
                tf.localScale = baseScale;
            }
        }
        private IEnumerator EnableBasketCollidersDelayed(bool b)
        {
            yield return new WaitForSeconds(0.5f);
            SetBasketColliders(b);
        }

        private void SetBasketColliders(bool enabled)
        {
            for (int i = 0; i < basketColliders.Count; i++)
            {
                basketColliders[i].enabled = enabled;
            }
        }
    }
}