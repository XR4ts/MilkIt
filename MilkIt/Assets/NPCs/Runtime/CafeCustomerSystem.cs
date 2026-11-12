using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    // Customers who arrive together, share a table and leave together.
    public sealed class CustomerParty
    {
        public int Id { get; }
        public CafeTable Table { get; }
        public List<CustomerController> Members { get; } = new List<CustomerController>();

        public CustomerParty(int id, CafeTable table) { Id = id; Table = table; }

        public bool AllDoneDrinking => Members.TrueForAll(m => m == null || m.DoneDrinking);

        public int SeatedCount
        {
            get
            {
                var count = 0;
                foreach (var m in Members) if (m != null && m.IsSeated) count++;
                return count;
            }
        }
    }

    public sealed class CafeCustomerSystem : MonoBehaviour
    {
        public Transform entrance;
        public Transform exit;
        [Tooltip("Optional point just inside the entrance, before joining the counter queue.")]
        public Transform entryWaypoint;
        public CafeSlidingDoor entranceDoor;
        [Tooltip("First marker is the ordering position; remaining markers extend toward the entrance.")]
        public Transform[] queuePositions;
        [Tooltip("Optional waypoint customers walk to right after ordering, so they do not walk back into the queue.")]
        public Transform tillExit;
        [Tooltip("All seats. Seats that belong to a table are reserved through their table.")]
        public CustomerSeat[] seats;
        [Tooltip("Optional. Parties sit together at one table.")]
        public CafeTable[] tables = new CafeTable[0];
        [Tooltip("When no table is empty, let a party use free seats at a partly occupied table.")]
        public bool shareTables = true;
        public CustomerController customerPrefab;
        [Tooltip("Optional character visuals (generated Humanoid prefabs). Empty keeps the capsules.")]
        public GameObject[] characterPrefabs = new GameObject[0];
        [Min(0.3f)] public float characterWalkSpeed = 1.2f;
        public string[] drinks = { "Espresso", "Latte", "Cappuccino" };
        [Min(0.1f)] public float orderSeconds = 2f;
        [Min(0.1f)] public float drinkingSeconds = 8f;
        [Range(1, 4)] public int maxPartySize = 4;
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
        int nextPartyId = 1;
        int partySize = 1;
        string feedback = "Spawn a customer to begin.";

        public bool TrySpawnCustomer() => TrySpawnParty(1);

        public bool TrySpawnParty(int size)
        {
            size = Mathf.Clamp(size, 1, Mathf.Max(1, maxPartySize));
            if (entrance == null || exit == null || customerPrefab == null ||
                queuePositions == null || queuePositions.Length == 0 || seats == null ||
                drinks == null || drinks.Length == 0)
                return Fail("Assign entrance, exit, queue, seats, prefab and drinks first.");
            foreach (var marker in queuePositions)
                if (marker == null) return Fail("A queue marker is missing.");
            if (queue.Count + size > queuePositions.Length)
                return Fail(size == 1 ? "The queue is full." : $"No room in the queue for {size} people.");
            var reserved = FindSeats(size, out var table);
            if (reserved == null)
                return Fail(size == 1 ? "All seats are reserved. Wait for someone to leave."
                    : $"No table has {size} free seats. Wait for someone to leave.");
            if (!NavMesh.SamplePosition(entrance.position, out var hit, 1f, NavMesh.AllAreas))
                return Fail("Entrance is not on a NavMesh. Check the navigation setup.");

            var party = size > 1 ? new CustomerParty(nextPartyId++, table) : null;
            var firstId = nextOrderId;
            // Different models inside a party so friends do not look the same.
            var looks = new List<int>();
            if (characterPrefabs != null)
                for (var i = 0; i < characterPrefabs.Length; i++) looks.Insert(UnityEngine.Random.Range(0, looks.Count + 1), i);
            for (var i = 0; i < size; i++)
            {
                var offset = entrance.right * ((i - (size - 1) * 0.5f) * 0.7f);
                var position = NavMesh.SamplePosition(hit.position + offset, out var spot, 0.6f, NavMesh.AllAreas)
                    ? spot.position : hit.position;
                var customer = Instantiate(customerPrefab, position, entrance.rotation, transform);
                customer.gameObject.SetActive(true);
                if (!reserved[i].TryReserve(customer))
                {
                    Destroy(customer.gameObject);
                    return Fail("Seat reservation failed.");
                }
                if (characterPrefabs != null && characterPrefabs.Length > 0)
                {
                    var prefab = characterPrefabs[looks[i % looks.Count]];
                    if (prefab != null) customer.AttachCharacter(prefab, characterWalkSpeed);
                }
                customers.Add(customer);
                queue.Add(customer);
                party?.Members.Add(customer);
                int id = nextOrderId++;
                customer.Begin(this, reserved[i], id, drinks[(id - 1) % drinks.Length], party, i * 0.8f);
            }
            feedback = size == 1 ? $"Customer {firstId} arrived."
                : $"Party {party.Id} arrived: customers {firstId}–{nextOrderId - 1}.";
            return true;
        }

        // Seats for a party, in queue order: inner booth seats go to the first arrivals so nobody
        // has to slide past a seated friend.
        CustomerSeat[] FindSeats(int size, out CafeTable table)
        {
            table = null;
            if (tables != null && tables.Length > 0)
            {
                CustomerSeat[] shared = null;
                CafeTable sharedTable = null;
                foreach (var candidate in tables)
                {
                    if (candidate == null) continue;
                    var picked = PickAtTable(candidate, size);
                    if (picked == null) continue;
                    if (candidate.Occupied == 0) { table = candidate; return picked; }
                    if (shared == null && shareTables) { shared = picked; sharedTable = candidate; }
                }
                table = sharedTable;
                if (shared != null) return shared;
            }
            if (size > 1) return null;
            // Seats without a table (e.g. the capsule test room).
            var single = Array.Find(seats, s => s != null && s.table == null && s.IsAccessible);
            return single != null ? new[] { single } : null;
        }

        static CustomerSeat[] PickAtTable(CafeTable table, int size)
        {
            var picked = new List<CustomerSeat>();
            foreach (var seat in table.seats)
            {
                if (picked.Count == size) break;
                // An inner seat is usable if its aisle seat is free now (or is being taken by this party).
                if (seat == null || seat.Occupant != null || !seat.IsValid) continue;
                if (seat.blockedBy != null && seat.blockedBy.Occupant != null) continue;
                picked.Add(seat);
            }
            if (picked.Count < size) return null;
            picked.Sort((a, b) => b.IsInner.CompareTo(a.IsInner));
            return picked.ToArray();
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
            if (tables != null && tables.Length > 0 && maxPartySize > 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Party size", GUILayout.Width(70));
                var labels = new string[maxPartySize];
                for (var i = 0; i < labels.Length; i++) labels[i] = (i + 1).ToString();
                partySize = GUILayout.Toolbar(Mathf.Clamp(partySize, 1, maxPartySize) - 1, labels) + 1;
                GUILayout.EndHorizontal();
                if (GUILayout.Button(partySize == 1 ? "Spawn customer" : $"Spawn party of {partySize}"))
                    TrySpawnParty(partySize);
            }
            else if (GUILayout.Button("Spawn customer")) TrySpawnCustomer();
            autoServe = GUILayout.Toggle(autoServe, "Automatically serve waiting customers");
            var free = 0;
            if (seats != null) foreach (var s in seats) if (s != null && s.Occupant == null) free++;
            GUILayout.Label($"Active: {customers.Count}    Completed: {CompletedVisits}    Free seats: {free}");
            GUILayout.Label(feedback);
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                GUILayout.Space(8);
                var group = customer.Party != null ? $" · party {customer.Party.Id}" : "";
                GUILayout.Label($"#{customer.OrderId} · {customer.Drink} · {customer.State}{group}");
                if (customer.State == CustomerState.WaitingForDrink &&
                    GUILayout.Button($"Serve {customer.Drink} to #{customer.OrderId}"))
                    TryDeliverDrink(customer.OrderId, customer.Drink);
            }
            GUILayout.EndArea();
            var view = Camera.main;
            if (view == null) return;
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                var point = view.WorldToScreenPoint(customer.LabelPosition);
                if (point.z > 0)
                    GUI.Box(new Rect(point.x - 70, Screen.height - point.y - 12, 140, 24),
                        $"#{customer.OrderId} · {customer.State}");
            }
        }
    }
}
