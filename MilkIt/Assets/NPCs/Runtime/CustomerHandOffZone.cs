using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.NPCs
{
    // Invisible trigger around a seated customer and their half of the table. While they wait for their drink,
    // any physical object (something with a Rigidbody, e.g. a grabbable cup) that enters it is reported once
    // through CafeCustomerSystem.DrinkHandedOver. Grading the drink and calling TryDeliverDrink is up to the
    // drink-making code.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CustomerHandOffZone : MonoBehaviour
    {
        CustomerController customer;
        CafeCustomerSystem cafe;
        readonly HashSet<GameObject> reported = new HashSet<GameObject>();

        public void Init(CustomerController owner, CafeCustomerSystem system)
        {
            customer = owner;
            cafe = system;
            var zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
            // A kinematic body so trigger events fire even if the cup's own Rigidbody is kinematic while held.
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public void Enable(bool active)
        {
            reported.Clear();
            gameObject.SetActive(active);
        }

        void OnTriggerEnter(Collider other)
        {
            if (customer == null || cafe == null || customer.State != CustomerState.WaitingForDrink) return;
            var body = other.attachedRigidbody;
            if (body == null) return;
            var item = body.gameObject;
            // Ignore the customer's own colliders and other customers.
            if (item.GetComponentInParent<CustomerController>() != null) return;
            if (!reported.Add(item)) return;
            cafe.ReportHandOver(customer, item);
        }

        void OnTriggerExit(Collider other)
        {
            // Taking the cup back out and offering it again counts as a new hand-over.
            if (other.attachedRigidbody != null) reported.Remove(other.attachedRigidbody.gameObject);
        }

        void OnDrawGizmosSelected()
        {
            var zone = GetComponent<BoxCollider>();
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(zone.center, zone.size);
        }
    }
}
