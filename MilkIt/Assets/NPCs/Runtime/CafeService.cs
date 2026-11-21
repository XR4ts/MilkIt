using System;
using UnityEngine;

namespace MilkIt.NPCs
{
    // Opening hours. While closed the café stays empty (practice mode). While open, parties arrive at random
    // intervals up to a customer limit. Closing stops new arrivals; customers inside finish and pay.
    [RequireComponent(typeof(CafeCustomerSystem))]
    public sealed class CafeService : MonoBehaviour
    {
        [Header("Arrivals while open (set per level)")]
        [Tooltip("Random seconds between parties arriving (min, max).")]
        public Vector2 arrivalInterval = new Vector2(10f, 20f);
        [Tooltip("Seconds after opening until the first party arrives.")]
        [Min(0f)] public float firstArrivalDelay = 3f;
        [Tooltip("No new party arrives while this many customers are inside.")]
        [Min(1)] public int maxCustomers = 12;
        [Tooltip("Relative chance of a party of 1, 2, 3 and 4.")]
        public float[] partySizeWeights = { 0.45f, 0.3f, 0.15f, 0.1f };
        public bool openOnStart;

        public bool IsOpen { get; private set; }
        public event Action<bool> OpenChanged;

        CafeCustomerSystem cafe;
        float nextArrival;
        const float RetrySeconds = 2f;

        void Awake() => cafe = GetComponent<CafeCustomerSystem>();

        void Start()
        {
            if (openOnStart) Open();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            nextArrival = Time.time + firstArrivalDelay;
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OpenChanged?.Invoke(false);
        }

        void Update()
        {
            if (!IsOpen || Time.time < nextArrival) return;
            var size = PickPartySize();
            // Full café or full queue: try again shortly instead of skipping a whole interval.
            if (cafe.Customers.Count + size > maxCustomers || !cafe.TrySpawnParty(size))
            {
                nextArrival = Time.time + RetrySeconds;
                return;
            }
            nextArrival = Time.time + UnityEngine.Random.Range(arrivalInterval.x, Mathf.Max(arrivalInterval.x, arrivalInterval.y));
        }

        int PickPartySize()
        {
            var count = Mathf.Min(cafe.maxPartySize, partySizeWeights != null ? partySizeWeights.Length : 0);
            if (count <= 1) return 1;
            var total = 0f;
            for (var i = 0; i < count; i++) total += Mathf.Max(0f, partySizeWeights[i]);
            var roll = UnityEngine.Random.value * total;
            for (var i = 0; i < count; i++)
            {
                roll -= Mathf.Max(0f, partySizeWeights[i]);
                if (roll <= 0f) return i + 1;
            }
            return 1;
        }
    }
}
