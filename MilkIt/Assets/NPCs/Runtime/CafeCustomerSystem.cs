using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    public sealed class CafeCustomerSystem : MonoBehaviour
    {
        public Transform entrance;
        public Transform exit;
        [Tooltip("Optional point just inside the entrance, before joining the counter queue.")]
        public Transform entryWaypoint;
        public CafeSlidingDoor entranceDoor;
        [Tooltip("First marker is the ordering position; remaining markers extend toward the entrance.")]
        public Transform[] queuePositions;
        public CustomerSeat[] seats;
        public CustomerController customerPrefab;
        public string[] drinks = { "Espresso", "Latte", "Cappuccino" };
        [Min(0.1f)] public float orderSeconds = 2f;
        [Min(0.1f)] public float drinkingSeconds = 8f;
        public bool autoServe;
        public bool showDebugPanel = true;

        // The order ID identifies one visit, even when multiple customers want the same drink.
        public event Action<int, string> OrderPlaced;
        public event Action<int> CustomerLeft;
        public int CompletedVisits { get; private set; }
        public IReadOnlyList<CustomerController> Customers => customers;
        readonly List<CustomerController> customers = new List<CustomerController>();
        readonly List<CustomerController> queue = new List<CustomerController>();
        int nextOrderId = 1;
        string feedback = "Spawn a customer to begin.";

        public bool TrySpawnCustomer()
        {
            if (entrance == null || exit == null || customerPrefab == null ||
                queuePositions == null || queuePositions.Length == 0 || seats == null ||
                drinks == null || drinks.Length == 0)
                return Fail("Assign entrance, exit, queue, seats, prefab and drinks first.");
            foreach (var marker in queuePositions)
                if (marker == null) return Fail("A queue marker is missing.");
            if (queue.Count >= queuePositions.Length) return Fail("The queue is full.");
            CustomerSeat available = Array.Find(seats, seat => seat != null &&
                seat.Occupant == null && seat.approach != null && seat.sittingPose != null);
            if (available == null) return Fail("All seats are reserved. Wait for someone to leave.");
            if (!NavMesh.SamplePosition(entrance.position, out var hit, 1f, NavMesh.AllAreas))
                return Fail("Entrance is not on a NavMesh. Check the navigation setup.");

            var customer = Instantiate(customerPrefab, hit.position, entrance.rotation, transform);
            customer.gameObject.SetActive(true);
            if (!available.TryReserve(customer))
            {
                Destroy(customer.gameObject);
                return Fail("Seat reservation failed.");
            }
            customers.Add(customer);
            queue.Add(customer);
            int id = nextOrderId++;
            customer.Begin(this, available, id, drinks[(id - 1) % drinks.Length]);
            feedback = $"Customer {id} arrived.";
            return true;
        }

        bool Fail(string message) { feedback = message; return false; }

        public Transform QueueTarget(CustomerController customer)
        {
            int index = queue.IndexOf(customer);
            return index >= 0 && index < queuePositions.Length ? queuePositions[index] : null;
        }

        public bool IsFirst(CustomerController customer) => queue.Count > 0 && queue[0] == customer;

        internal void PlaceOrder(CustomerController customer)
        {
            queue.Remove(customer);
            feedback = $"Customer {customer.OrderId} ordered {customer.Drink}.";
            OrderPlaced?.Invoke(customer.OrderId, customer.Drink);
        }

        // Call this from the team's serving system once a drink is delivered to this customer.
        // False means an unknown order, wrong drink, or customer not ready to receive it.
        public bool TryDeliverDrink(int orderId, string drink)
        {
            var customer = customers.Find(c => c != null && c.OrderId == orderId);
            return customer != null && customer.TryReceiveDrink(drink);
        }

        internal void Remove(CustomerController customer, bool completed)
        {
            queue.Remove(customer);
            if (!customers.Remove(customer)) return;
            if (completed)
            {
                CompletedVisits++;
                CustomerLeft?.Invoke(customer.OrderId);
            }
        }

        void Update()
        {
            if (!autoServe) return;
            foreach (var customer in customers)
                if (customer != null && customer.State == CustomerState.WaitingForDrink)
                    TryDeliverDrink(customer.OrderId, customer.Drink);
        }

        void OnGUI()
        {
            if (!showDebugPanel) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, Mathf.Max(200, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("MILKIT / CUSTOMER PROTOTYPE");
            GUILayout.Label("Enter → Queue → Order → Sit → Drink → Leave");
            if (GUILayout.Button("Spawn customer")) TrySpawnCustomer();
            autoServe = GUILayout.Toggle(autoServe, "Automatically serve waiting customers");
            GUILayout.Label($"Active: {customers.Count}    Completed: {CompletedVisits}");
            GUILayout.Label(feedback);
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                GUILayout.Space(8);
                GUILayout.Label($"#{customer.OrderId} · {customer.Drink} · {customer.State}");
                if (customer.State == CustomerState.WaitingForDrink &&
                    GUILayout.Button($"Serve {customer.Drink} to #{customer.OrderId}"))
                    TryDeliverDrink(customer.OrderId, customer.Drink);
            }
            GUILayout.EndArea();
            var view = Camera.main;
            if (view == null) return;
            foreach (var customer in customers)
            {
                if (customer == null || customer.body == null) continue;
                var point = view.WorldToScreenPoint(customer.body.position + Vector3.up * 1.2f);
                if (point.z > 0)
                    GUI.Box(new Rect(point.x - 70, Screen.height - point.y - 12, 140, 24),
                        $"#{customer.OrderId} · {customer.State}");
            }
        }
    }
}
