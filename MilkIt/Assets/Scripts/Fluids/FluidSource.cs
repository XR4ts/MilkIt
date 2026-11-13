using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    public class FluidSource : MonoBehaviour
    {
        [Header("Output")]
        public FluidType fluidType;
        [Tooltip("ml per second at full flow.")]
        public float flowRate = 60f;
        [Tooltip("Where the stream starts. Defaults to this transform.")]
        public Transform spout;
        public bool startOn;

        [Header("Dose")]
        [Tooltip("ml produced by one Dispense() signal.")]
        public float doseAmount = 40f;

        [Header("Supply (optional)")]
        [Tooltip("If set, output is limited by what this container holds, and uses it up.")]
        public FluidContainer supply;
        [Tooltip("Material taken from the supply. Empty = whatever is in there.")]
        public FluidType supplyType;
        [Tooltip("ml of supply used per ml of output.")]
        public float supplyPerOutput = 1f;

        [Header("Events")]
        public UnityEvent onStarted = new UnityEvent();
        public UnityEvent onStopped = new UnityEvent();
        public UnityEvent onDoseCompleted = new UnityEvent();
        public UnityEvent onSupplyEmpty = new UnityEvent();

        FluidStream stream;
        float remaining;
        float flowScale = 1f;

        public bool IsFlowing { get; private set; }

        void Start()
        {
            if (startOn) StartFlow();
        }

        public void StartFlow() => Begin(float.PositiveInfinity);

        public void StopFlow()
        {
            if (!IsFlowing) return;
            IsFlowing = false;
            onStopped.Invoke();
        }

        public void Toggle()
        {
            if (IsFlowing) StopFlow();
            else StartFlow();
        }

        public void Dispense() => Begin(doseAmount);

        public void DispenseAmount(float ml) => Begin(ml);

        public void SetFlowScale(float value) => flowScale = Mathf.Clamp01(value);

        void Begin(float amount)
        {
            if (amount <= 0f) return;
            remaining = amount;
            if (IsFlowing) return;
            IsFlowing = true;
            onStarted.Invoke();
        }

        void Update()
        {
            if (!IsFlowing || fluidType == null) return;

            float amount = Mathf.Min(flowRate * flowScale * Time.deltaTime, remaining);
            if (supply != null && supplyPerOutput > 0f)
            {
                float available = supplyType != null ? supply.GetAmount(supplyType) : supply.Total;
                amount = Mathf.Min(amount, available / supplyPerOutput);
                if (amount <= 0.0001f)
                {
                    onSupplyEmpty.Invoke();
                    StopFlow();
                    return;
                }
                if (supplyType != null) supply.Remove(supplyType, amount * supplyPerOutput);
                else supply.Remove(amount * supplyPerOutput);
            }
            if (amount <= 0f) return;

            if (stream == null) stream = FluidStream.Create(gameObject, null);
            stream.SetOrigin(spout != null ? spout.position : transform.position);
            stream.Add(fluidType, amount);

            remaining -= amount;
            if (remaining <= 0.0001f)
            {
                StopFlow();
                onDoseCompleted.Invoke();
            }
        }
    }
}
