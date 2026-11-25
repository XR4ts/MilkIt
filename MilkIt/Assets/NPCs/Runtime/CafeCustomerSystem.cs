using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using MilkIt.Machine.DrinkGrader;
using MilkIt.Machine;
using MilkIt.Fluids;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
        [Min(0.1f)] public float orderSeconds = 2f;
        [Min(0.1f)] public float drinkingSeconds = 8f;
        [Range(1, 4)] public int maxPartySize = 4;

        [Header("Patience and payment (set per level)")]
        [Tooltip("Seconds a seated customer waits for their drink before leaving without paying. 0 = unlimited.")]
        [Min(0f)] public float patienceSeconds = 60f;
        [Tooltip("Fraction of patience left when the customer starts looking frustrated.")]
        [Range(0f, 1f)] public float frustratedAt = 0.35f;
        [Tooltip("Price per drink, in the same order as Drinks. Missing entries use Default Price.")]
        [Min(0)] public int defaultPrice = 3;
        [Tooltip("Optional marker above each customer's head (patience bar, payment popup).")]
        public CustomerPatienceIndicator patienceIndicatorPrefab;

        [Header("Ordering at the till")]
        [Tooltip("Customers wait at the register until the player selects them (ray, poke or click).")]
        public bool requireOrderClick;
        [Tooltip("Debug: take orders automatically instead of waiting for the player.")]
        public bool autoTakeOrders;
        [Tooltip("Optional speech bubble that shows the order above the customer.")]
        public CustomerOrderBubble orderBubblePrefab;
        public bool autoServe;
        public bool showDebugPanel = true;
        [Tooltip("Off: the panel only shows Serve buttons for customers waiting for their drink.")]
        public bool fullDebugPanel = true;

        public int[] drinkPrices;
        public Texture2D[] drinkIcons;
        public string[] drinks;

        // The order ID identifies one visit, even when multiple customers want the same drink.
        public event Action<int, string> OrderPlaced;
        public event Action<int> CustomerLeft;
        // orderId, amount. Raised when a customer finishes their drink and pays.
        public event Action<int, int> CustomerPaid;
        // orderId. Raised when a customer's patience runs out; they leave without paying.
        public event Action<int> CustomerGaveUp;
        // (customer, object) Raised when a physical object (anything with a Rigidbody, e.g. a cup) is brought to a
        // seated customer who is waiting for their drink: into their hand-off zone over them and their half of the
        // table. Once per object per visit to the zone. Grade it and call TryDeliverDrink to serve.
        public event Action<CustomerController, GameObject> DrinkHandedOver;
        public int CompletedVisits { get; private set; }
        public int Earnings { get; private set; }
        public int GaveUpCount { get; private set; }
        public IReadOnlyList<CustomerController> Customers => customers;
        readonly List<CustomerController> customers = new List<CustomerController>();
        readonly List<CustomerController> queue = new List<CustomerController>();
        int nextOrderId = 1;
        int nextPartyId = 1;
        int partySize = 1;
        float entranceFreeAt;
        const float ArrivalSpacing = 0.9f;
        string feedback = "Spawn a customer to begin.";

        void Start()
        {
            DrinkType[] drinkTypes = Resources.LoadAll<DrinkType>("DrinkTypes");
            drinks = drinkTypes.Select(d => d.name).ToArray();
            drinkPrices = drinkTypes.Select(d => d.price).ToArray();
            drinkIcons = drinkTypes.Select(d => d.icon).ToArray();
            DrinkHandedOver += (customerController, gameObject) =>
            {
                if (!gameObject.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer))
                    return;
                (float drinkScore, DrinkType predictedDrink) = DrinkGrader.GradeDrink(fluidContainer);
                string drinkName = predictedDrink.name; // Why are we operating on magic fucking strings for names of drinks................... Love you Claude
                if (!TryDeliverDrink(customerController.OrderId, drinkName, Mathf.RoundToInt(drinkScore * PriceOf(drinkName)))) return;
                ForceRelease(gameObject);
                if (gameObject.TryGetComponent<Resetable>(out Resetable reset))
                    fluidContainer.Remove(fluidContainer.Total);
                    reset.ResetObject();
                Destroy(gameObject);
            };
        }

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
                // One customer sets off from the entrance at a time, even when several parties spawn at once.
                var startAt = Mathf.Max(Time.time, entranceFreeAt);
                entranceFreeAt = startAt + ArrivalSpacing;
                var customer = Instantiate(customerPrefab, hit.position, entrance.rotation, transform);
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
                customer.AttachIndicator(patienceIndicatorPrefab);
                customer.AttachOrderBubble(orderBubblePrefab);
                if (requireOrderClick) customer.MakeSelectable();
                customers.Add(customer);
                queue.Add(customer);
                party?.Members.Add(customer);
                int id = nextOrderId++;
                customer.Begin(this, reserved[i], id, drinks[UnityEngine.Random.Range(0, drinks.Length)], party, startAt - Time.time);
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

        public Texture2D IconOf(string drink)
        {
            var index = drinks != null ? Array.IndexOf(drinks, drink) : -1;
            return index >= 0 && drinkIcons != null && index < drinkIcons.Length ? drinkIcons[index] : null;
        }

        // Called when the player selects a customer. Only the customer waiting at the till can order.
        public bool TakeOrder(CustomerController customer)
        {
            if (customer == null || !customer.TryTakeOrder()) return false;
            feedback = $"Taking order from customer {customer.OrderId}.";
            return true;
        }

        public int PriceOf(string drink)
        {
            var index = drinks != null ? Array.IndexOf(drinks, drink) : -1;
            return index >= 0 && drinkPrices != null && index < drinkPrices.Length ? drinkPrices[index] : defaultPrice;
        }

        internal int Pay(CustomerController customer, int amount)
        {
            Earnings += amount;
            feedback = $"Customer {customer.OrderId} paid {amount} for {customer.Drink}.";
            CustomerPaid?.Invoke(customer.OrderId, amount);
            return amount;
        }

        internal void ReportHandOver(CustomerController customer, GameObject item)
        {
            feedback = $"{item.name} handed to customer {customer.OrderId} ({customer.Drink}).";
            DrinkHandedOver?.Invoke(customer, item);
        }

        internal void GiveUp(CustomerController customer)
        {
            GaveUpCount++;
            feedback = $"Customer {customer.OrderId} waited too long and left without paying.";
            CustomerGaveUp?.Invoke(customer.OrderId);
        }

        // Call this from the team's serving system once a drink is delivered to this customer.
        // False means an unknown order, wrong drink, or customer not ready to receive it.
        public bool TryDeliverDrink(int orderId, string drink, int drinkPrice)
        {
            var customer = customers.Find(c => c != null && c.OrderId == orderId);
            return customer != null && customer.TryReceiveDrink(drink, drinkPrice);
        }

        public bool TryDeliverDrink(int orderId, string drink)
        {
            var customer = customers.Find(c => c != null && c.OrderId == orderId);
            return customer != null && customer.TryReceiveDrink(drink, PriceOf(drink));
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
            if (!autoServe && !autoTakeOrders) return;
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                if (autoServe && customer.State == CustomerState.WaitingForDrink)
                    TryDeliverDrink(customer.OrderId, customer.Drink, PriceOf(customer.Drink));
                if (autoTakeOrders && customer.State == CustomerState.WaitingAtTill)
                    TakeOrder(customer);
            }
        }
        private void ForceRelease(GameObject gameObject)
        {
            XRGrabInteractable grab = gameObject.GetComponent<XRGrabInteractable>();
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

        // Minimal panel until drink making is in the scene: one button per customer waiting for their drink.
        void ServePanel()
        {
            var waiting = customers.FindAll(c => c != null && c.State == CustomerState.WaitingForDrink);
            if (waiting.Count == 0) return;
            GUILayout.BeginArea(new Rect(12, 12, 260, 34 + waiting.Count * 26), GUI.skin.box);
            GUILayout.Label("Serve drinks (debug)");
            foreach (var customer in waiting)
                if (GUILayout.Button($"Serve {customer.Drink} to #{customer.OrderId}"))
                    TryDeliverDrink(customer.OrderId, customer.Drink);
            GUILayout.EndArea();
        }

        void OnGUI()
        {
            if (!showDebugPanel) return;
            if (!fullDebugPanel) { ServePanel(); return; }
            GUILayout.BeginArea(new Rect(12, 12, 330, Mathf.Max(200, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("MILKIT / CUSTOMER PROTOTYPE");
            GUILayout.Label("Enter → Queue → Order → Sit → Drink → Leave");
            var service = GetComponent<CafeService>();
            if (service != null && GUILayout.Button(service.IsOpen ? "Close café (sign)" : "Open café (sign)"))
                service.Toggle();
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
            if (requireOrderClick) autoTakeOrders = GUILayout.Toggle(autoTakeOrders, "Automatically take orders at the till");
            var free = 0;
            if (seats != null) foreach (var s in seats) if (s != null && s.Occupant == null) free++;
            GUILayout.Label($"Active: {customers.Count}    Completed: {CompletedVisits}    Free seats: {free}");
            GUILayout.Label($"Earnings: {Earnings}    Left unpaid: {GaveUpCount}    Patience: {(patienceSeconds > 0f ? patienceSeconds + " s" : "unlimited")}");
            GUILayout.Label(feedback);
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                GUILayout.Space(8);
                var group = customer.Party != null ? $" · party {customer.Party.Id}" : "";
                var wait = customer.State == CustomerState.WaitingForDrink && patienceSeconds > 0f
                    ? $" · {Mathf.CeilToInt(customer.PatienceSecondsLeft)} s{(customer.Frustrated ? " !" : "")}" : "";
                GUILayout.Label($"#{customer.OrderId} · {customer.Drink} ({PriceOf(customer.Drink)}) · {customer.State}{wait}{group}");
                if (customer.State == CustomerState.WaitingAtTill &&
                    GUILayout.Button($"Take order from #{customer.OrderId}"))
                    TakeOrder(customer);
                if (customer.State == CustomerState.WaitingForDrink &&
                    GUILayout.Button($"Serve {customer.Drink} to #{customer.OrderId}"))
                    TryDeliverDrink(customer.OrderId, customer.Drink);
            }
            GUILayout.EndArea();
        }
    }
}
