using System.Collections.Generic;
using UnityEngine;
using MilkIt.NPCs;

namespace MilkIt.Audio
{
    public class CafeFeedbackSounds : MonoBehaviour
    {
        public enum AcceptedMoment { DrinkServed, OrderTaken }

        [Tooltip("Leave empty to find the café automatically.")]
        [SerializeField] CafeCustomerSystem cafe;

        [SerializeField] AudioSource cashRegister;
        [SerializeField] AudioSource accepted;
        [SerializeField] AudioSource fail;

        [Tooltip("DrinkServed: a waiting customer gets the right drink. OrderTaken: you take an order at the till.")]
        [SerializeField] AcceptedMoment acceptedWhen = AcceptedMoment.DrinkServed;

        [Tooltip("How high above the customer's feet Accepted and Fail play from (about head height when seated).")]
        [SerializeField] float heightAboveCustomer = 1.3f;

        [Tooltip("Random pitch change each time, so repeated sounds don't sound identical.")]
        [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.03f;

        Dictionary<CustomerController, CustomerState> previous = new Dictionary<CustomerController, CustomerState>();
        Dictionary<CustomerController, CustomerState> current = new Dictionary<CustomerController, CustomerState>();

        void Start()
        {
            // The NPC setup menu rebuilds the café objects, so we look them up instead of relying on a saved link.
            if (cafe == null) cafe = FindFirstObjectByType<CafeCustomerSystem>();
            if (cafe == null)
            {
                Debug.LogWarning("CafeFeedbackSounds: no CafeCustomerSystem found.", this);
                enabled = false;
                return;
            }
            cafe.CustomerPaid += OnCustomerPaid;
        }

        void OnDestroy()
        {
            if (cafe != null) cafe.CustomerPaid -= OnCustomerPaid;
        }

        void OnCustomerPaid(int orderId, int amount)
        {
            Play(cashRegister);
        }

        void Update()
        {
            current.Clear();
            foreach (var customer in cafe.Customers)
            {
                if (customer == null) continue;
                CustomerState state = customer.State;
                if (previous.TryGetValue(customer, out CustomerState before) && before != state)
                    OnStateChanged(customer, before, state);
                current[customer] = state;
            }
            (previous, current) = (current, previous);
        }

        void OnStateChanged(CustomerController customer, CustomerState before, CustomerState now)
        {
            bool drinkServed = before == CustomerState.WaitingForDrink && now == CustomerState.Drinking;
            bool orderTaken = before == CustomerState.WaitingAtTill && now == CustomerState.Ordering;

            if ((acceptedWhen == AcceptedMoment.DrinkServed && drinkServed) ||
                (acceptedWhen == AcceptedMoment.OrderTaken && orderTaken))
                PlayAt(accepted, customer);

            if (now == CustomerState.LeavingAngry)
                PlayAt(fail, customer);
        }

        void PlayAt(AudioSource source, CustomerController customer)
        {
            if (source == null) return;
            source.transform.position = customer.transform.position + Vector3.up * heightAboveCustomer;
            Play(source);
        }

        void Play(AudioSource source)
        {
            if (source == null) return;
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }
    }
}