using UnityEngine;

namespace MilkIt.NPCs
{
    public sealed class CustomerSeat : MonoBehaviour
    {
        public Transform approach;
        [Tooltip("Capsules: where the body goes. Characters: floor point and facing for the character root.")]
        public Transform sittingPose;
        [Tooltip("Optional table this seat belongs to.")]
        public CafeTable table;
        [Tooltip("Aisle-side seat a customer must pass to reach this one (booth inner seat).")]
        public CustomerSeat blockedBy;
        public CustomerController Occupant { get; private set; }

        public bool IsValid => approach != null && sittingPose != null;
        public bool IsInner => blockedBy != null;

        // Free, and not behind someone else's reserved aisle seat.
        public bool IsAccessible => Occupant == null && IsValid && (blockedBy == null || blockedBy.Occupant == null);

        public bool TryReserve(CustomerController customer)
        {
            if (customer == null || Occupant != null || !IsValid)
                return false;
            Occupant = customer;
            return true;
        }

        public void Release(CustomerController customer)
        {
            if (Occupant == customer) Occupant = null;
        }
    }
}
