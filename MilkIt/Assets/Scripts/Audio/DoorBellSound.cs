using System.Collections.Generic;
using UnityEngine;
using MilkIt.NPCs;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class DoorBellSound : MonoBehaviour
    {
        [Tooltip("Leave empty to find the café's customer system automatically.")]
        [SerializeField] CafeCustomerSystem cafe;

        [Tooltip("Customers coming in within this many seconds of each other (a party) share one ring. 0 = ring for everyone.")]
        [SerializeField, Min(0f)] float groupSeconds = 2f;

        [Tooltip("Random pitch change each time, so the bell doesn't sound identical every time.")]
        [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.03f;

        AudioSource source;
        Transform door;
        Vector3 inward;
        float lastEntry = float.NegativeInfinity;

        Dictionary<CustomerController, bool> previous = new Dictionary<CustomerController, bool>();
        Dictionary<CustomerController, bool> current = new Dictionary<CustomerController, bool>();

        void Start()
        {
            source = GetComponent<AudioSource>();

            if (cafe == null) cafe = FindFirstObjectByType<CafeCustomerSystem>();
            if (cafe == null || cafe.entranceDoor == null || cafe.entrance == null)
            {
                Debug.LogWarning("DoorBellSound: no café customer system with an entrance door found.", this);
                enabled = false;
                return;
            }

            door = cafe.entranceDoor.transform;
            inward = Vector3.ProjectOnPlane(door.position - cafe.entrance.position, Vector3.up).normalized;
        }

        void Update()
        {
            current.Clear();

            foreach (var customer in cafe.Customers)
            {
                if (customer == null) continue;

                bool inside = Vector3.Dot(customer.transform.position - door.position, inward) > 0f;
                bool justWalkedIn = previous.TryGetValue(customer, out bool wasInside)
                                    && !wasInside && inside
                                    && customer.State == CustomerState.Entering;

                if (justWalkedIn)
                {
                    if (Time.time - lastEntry > groupSeconds) Ring();
                    lastEntry = Time.time;
                }

                current[customer] = inside;
            }

            // Swap the two lists; customers who have left the café are dropped automatically.
            (previous, current) = (current, previous);
        }

        void Ring()
        {
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }
    }
}