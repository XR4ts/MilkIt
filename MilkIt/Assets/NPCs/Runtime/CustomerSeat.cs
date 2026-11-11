using UnityEngine;

namespace MilkIt.NPCs
{
    public sealed class CustomerSeat : MonoBehaviour
    {
        public Transform approach;
        public Transform sittingPose;
        public CustomerController Occupant { get; private set; }

        public bool TryReserve(CustomerController customer)
        {
            if (customer == null || Occupant != null || approach == null || sittingPose == null)
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
