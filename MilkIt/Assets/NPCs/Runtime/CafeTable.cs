using UnityEngine;

namespace MilkIt.NPCs
{
    // A table whose seats a party reserves together. Seats are ordered so the aisle-side seats come first.
    public sealed class CafeTable : MonoBehaviour
    {
        public CustomerSeat[] seats = new CustomerSeat[0];

        public int Occupied
        {
            get
            {
                var count = 0;
                foreach (var seat in seats) if (seat != null && seat.Occupant != null) count++;
                return count;
            }
        }
    }
}
