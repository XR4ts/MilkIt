using UnityEngine;
using MilkIt;
using MilkIt.Fluids;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Linq;

namespace MilkIt.Machine.CoffeeMachine{
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
    
            // Debug.Log($"Object={other} entered trigger");
            if (!other.TryGetComponent<Flask>(out Flask flask))
                return;

            // Debug.Log($"Flask={flask} entered trigger");
            AttachFlask(flask);
        }
    
        private void AttachFlask(Flask flask)
        {
            if (!flask.CanSnap)
            {
                return;
            }
            ForceRelease(flask); // release the flask from player grab

            flask.WhenConnected(true);
            
            currentFlask = flask;
            flask.SetReceiver(this);
            
            Rigidbody rb = flask.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
    
            flask.transform.SetParent(attachmentPoint);
            flask.transform.position = attachmentPoint.position;
    
            // Activate this receiver's fluid source.
            if (manages != null)
            {
                manages.fluidSource = flask.fluidSource;
            }
            flask.StartSnapCooldown();

            // Debug.Log($"Attached {flask} to {attachmentPoint}");
        }
    
        public void RemoveFlask(Flask flask, IXRSelectInteractor interactor)
        {
            ForceRelease(flask); // release the flask to manipulate rigidbody and parent

            currentFlask = null;
            flask.WhenConnected(false);
            if (manages != null)
            {
                manages.fluidSource = null;   
                flask.fluidSource.SetFlowScale(0);
            }   
            flask.SetReceiver(null);
    
            Rigidbody rb = flask.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
            flask.StartSnapCooldown();

            ForceGrab(flask, interactor);
            // Debug.Log($"Dettached {flask} to {flask.defaultParent}");
        }

        private void ForceRelease(Flask flask)
        {
            XRGrabInteractable grab = flask.GetComponent<XRGrabInteractable>();
            if (grab == null || !grab.isSelected)
                return;

            var manager = grab.interactionManager;
            if (manager == null)
                return;

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