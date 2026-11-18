using UnityEngine;
using MilkIt;
using MilkIt.Fluids;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Linq;

namespace MilkIt.Machine.CoffeeMachine{
    //TODO: figure out what to do with the colliders, rebuild the body? Make a list of colliders to disable?
    // TODO: now a list of colliders is disabled, but that causes it to just fall out, idk maybe because of gravity??? idk
    public class FlaskDetector : MonoBehaviour
    {
        [Header("Receiver")]
        [Tooltip("Where the flask should be positioned.")]
        public Transform attachmentPoint;
	    [SerializeField] private SimpleLever manages; 
    
        private Flask currentFlask;
    
        private void OnTriggerEnter(Collider other)
        {
            if (currentFlask != null)
                return;
    
            if (!other.TryGetComponent<Flask>(out Flask flask))
                return;
    
            AttachFlask(flask);
        }
    
        private void AttachFlask(Flask flask)
        {
            if (!flask.CanSnap)
            {
                return;
            }
            Debug.Log("Attaching flask...");
            flask.WhenConnected(true);
            ForceRelease(flask);
            currentFlask = flask;
            flask.SetReceiver(this);
            // Suspend the flask in the receiver.
            Rigidbody rb = flask.GetComponent<Rigidbody>();
    
            if (rb != null)
            {
                rb.isKinematic = true;
            }
    
            flask.transform.SetParent(attachmentPoint);
            flask.transform.SetPositionAndRotation(
                attachmentPoint.position,
                attachmentPoint.rotation
            );
    
            // Activate this receiver's fluid source.
            manages.fluidSource = flask.fluidSource;
            flask.StartSnapCooldown();
            Debug.Log("Flask attached");
        }
    
        public void RemoveFlask(Flask flask, IXRSelectInteractor interactor)
        {
            Debug.Log("Detaching flask...");
            ForceRelease(flask);
            currentFlask = null;
            flask.WhenConnected(false);
    
            manages.fluidSource = null;
            flask.SetReceiver(null);
    
            Rigidbody rb = flask.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
            flask.StartSnapCooldown();
            ForceGrab(flask, interactor);
            Debug.Log("Flask detached");
        }

        private void ForceRelease(Flask flask)
        {
            XRGrabInteractable grab = flask.GetComponent<XRGrabInteractable>();

            if (grab == null || !grab.isSelected)
                return;

            var manager = grab.interactionManager;

            if (manager == null)
                return;

            // Release every interactor currently selecting the flask.
            foreach (var interactor in grab.interactorsSelecting.ToArray())
            {
                manager.SelectExit(interactor, grab);
            }
        }

        private void ForceGrab(Flask flask, IXRSelectInteractor interactor)
        {
            XRGrabInteractable grab = flask.GetComponent<XRGrabInteractable>();

            if (grab == null || grab.isSelected)
                return;

            var manager = grab.interactionManager;

            if (manager == null)
                return;

            manager.SelectEnter(interactor, grab);
        }
    }
}