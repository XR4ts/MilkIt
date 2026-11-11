using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    /// <summary>
    /// Anything that holds fluid: a cup, a machine's water tank, a drain, a trash can.
    /// Tracks how much of each material it holds (in pour order), pours out when tilted,
    /// and spills whatever it cannot take.
    /// </summary>
    public class FluidContainer : MonoBehaviour
    {
        /// <summary>All enabled containers. Streams and scoop zones search this instead of using physics.</summary>
        public static readonly List<FluidContainer> All = new List<FluidContainer>();

        [Header("Contents")]
        [Min(1f), Tooltip("Maximum volume in ml.")]
        public float capacity = 250f;
        [SerializeField, Tooltip("Current contents in ml, in the order each material was first poured in.")]
        List<FluidLayer> layers = new List<FluidLayer>();

        [Header("Shape (treated as a cylinder, in the opening's local units)")]
        [Tooltip("Centre of the rim, +Y pointing out of the container. Defaults to this transform.")]
        public Transform opening;
        public float openingRadius = 0.035f;
        [Tooltip("Distance from the rim down to the inside bottom.")]
        public float depth = 0.065f;

        [Header("Receiving")]
        public bool canReceive = true;
        [Tooltip("Empty = accepts everything. Anything else poured in is spilled.")]
        public List<FluidType> acceptedTypes = new List<FluidType>();
        [Tooltip("Swallows everything and never fills up (sink drain, trash can).")]
        public bool isDrain;
        [Tooltip("Fills up when its opening is dipped into a FluidScoopZone.")]
        public bool scoopable = true;

        [Header("Pouring")]
        [Tooltip("Pours out when tilted. Turn off for fixed tanks.")]
        public bool canPour = true;
        [Tooltip("ml per second through the opening.")]
        public float maxPourRate = 250f;

        [Header("Visual")]
        [Tooltip("Show a simple fill-level cylinder inside the container.")]
        public bool showFill = true;

        [Header("Events")]
        public UnityEvent onContentsChanged = new UnityEvent();
        public UnityEvent onFull = new UnityEvent();
        public UnityEvent onEmptied = new UnityEvent();
        [Tooltip("Something was spilled because it did not fit or was not accepted.")]
        public UnityEvent onOverflow = new UnityEvent();
        [Tooltip("Drains only: something was swallowed.")]
        public UnityEvent<FluidType, float> onDrained = new UnityEvent<FluidType, float>();

        public event Action<FluidContainer> ContentsChanged;

        float total;
        float lastSubmergedTime = -10f;
        FluidStream stream;
        MeshRenderer fillRenderer;

        public Transform Opening => opening != null ? opening : transform;
        public IReadOnlyList<FluidLayer> Layers => layers;
        public float Total => total;
        public float FillFraction => capacity > 0f ? Mathf.Clamp01(total / capacity) : 0f;
        public bool IsFull => total >= capacity - 0.01f;
        public bool IsEmpty => total <= 0f;
        public float WorldRadius => openingRadius * Opening.lossyScale.x;
        public float WorldDepth => depth * Opening.lossyScale.y;

        /// <summary>World height where a stream falling into this container should end.</summary>
        public float SurfaceWorldY => Opening.position.y - WorldDepth * (isDrain ? 1f : 1f - FillFraction);

        void Awake()
        {
            layers.RemoveAll(l => l.type == null || l.amount <= 0f);
            total = FluidMix.Total(layers);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Start() => UpdateFillVisual();

        // ---------------------------------------------------------------- queries

        public float GetAmount(FluidType type)
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].type == type) return layers[i].amount;
            return 0f;
        }

        /// <summary>Share of the container's capacity taken by this material (0..1).</summary>
        public float GetFractionOfCapacity(FluidType type) => capacity > 0f ? GetAmount(type) / capacity : 0f;

        /// <summary>Share of the current contents made up by this material (0..1).</summary>
        public float GetFractionOfContents(FluidType type) => total > 0f ? GetAmount(type) / total : 0f;

        public bool Accepts(FluidType type) =>
            type != null && (acceptedTypes == null || acceptedTypes.Count == 0 || acceptedTypes.Contains(type));

        // ---------------------------------------------------------------- changing contents

        /// <summary>Something poured in from above. Whatever does not fit or is not accepted is spilled over the rim.</summary>
        public void Receive(FluidType type, float ml)
        {
            if (type == null || ml <= 0f) return;
            if (!Accepts(type))
            {
                Spill(type, ml);
                return;
            }
            if (isDrain)
            {
                onDrained.Invoke(type, ml);
                return;
            }
            float accepted = Add(type, ml);
            if (ml - accepted > 0.0001f) Spill(type, ml - accepted);
        }

        /// <summary>Adds up to the free capacity without spilling. Returns the amount actually added.</summary>
        public float Add(FluidType type, float ml)
        {
            if (type == null || ml <= 0f || isDrain) return 0f;
            float accepted = Mathf.Min(ml, capacity - total);
            if (accepted <= 0f) return 0f;
            bool wasFull = IsFull;
            FluidMix.Add(layers, type, accepted);
            total += accepted;
            Changed();
            if (!wasFull && IsFull) onFull.Invoke();
            return accepted;
        }

        /// <summary>Removes an amount of the mix, taking every material proportionally. Returns the amount removed.</summary>
        public float Remove(float ml) => RemoveMix(ml, null);

        /// <summary>Removes an amount of one material. Returns the amount removed.</summary>
        public float Remove(FluidType type, float ml)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].type != type) continue;
                float taken = Mathf.Min(ml, layers[i].amount);
                if (taken <= 0f) return 0f;
                layers[i] = new FluidLayer(type, layers[i].amount - taken);
                AfterRemoval();
                return taken;
            }
            return 0f;
        }

        public void Empty()
        {
            if (layers.Count == 0) return;
            layers.Clear();
            AfterRemoval();
        }

        float RemoveMix(float ml, FluidStream into)
        {
            ml = Mathf.Min(ml, total);
            if (ml <= 0f) return 0f;
            float k = ml / total;
            for (int i = 0; i < layers.Count; i++)
            {
                FluidLayer l = layers[i];
                float take = l.amount * k;
                layers[i] = new FluidLayer(l.type, l.amount - take);
                if (into != null) into.Add(l.type, take);
            }
            AfterRemoval();
            return ml;
        }

        void AfterRemoval()
        {
            layers.RemoveAll(l => l.amount < 0.001f);
            total = FluidMix.Total(layers);
            if (total < 0.05f)
            {
                layers.Clear();
                total = 0f;
            }
            Changed();
            if (total <= 0f) onEmptied.Invoke();
        }

        void Changed()
        {
            onContentsChanged.Invoke();
            ContentsChanged?.Invoke(this);
            UpdateFillVisual();
        }

        // ---------------------------------------------------------------- pouring and spilling

        /// <summary>Called by scoop zones while the opening is under their surface; stops it pouring back out.</summary>
        public void MarkSubmerged() => lastSubmergedTime = Time.time;

        void Update()
        {
            if (!canPour || total <= 0f) return;
            if (Time.time - lastSubmergedTime < 0.1f) return;

            float excess = total - MaxFillAtTilt(Opening.up.y) * capacity;
            if (excess <= 0.01f) return;

            // Drain faster the further past the spill point it is tilted, limited by the opening.
            float rate = Mathf.Min(maxPourRate, Mathf.Max(excess * 6f, 15f));
            float amount = Mathf.Min(excess, rate * Time.deltaTime);
            FluidStream s = GetStream();
            s.SetOrigin(RimPoint());
            RemoveMix(amount, s);
        }

        /// <summary>
        /// Largest fill fraction the container can hold at this tilt before the surface reaches the rim.
        /// cosTilt is the dot product of the opening's up axis with world up.
        /// </summary>
        float MaxFillAtTilt(float cosTilt)
        {
            if (cosTilt >= 0.9999f) return 1f;
            if (cosTilt <= 0.02f) return 0f;
            float tan = Mathf.Sqrt(1f - cosTilt * cosTilt) / cosTilt;
            float a = openingRadius * tan / Mathf.Max(depth, 0.0001f);
            float f = 1f - a;                       // surface still covers the whole bottom
            return f >= 0.5f ? f : 1f / (4f * a);   // otherwise a wedge in the lower corner
        }

        /// <summary>Lowest point of the rim: where fluid leaves the container.</summary>
        Vector3 RimPoint()
        {
            Transform o = Opening;
            Vector3 up = o.up;
            Vector3 dir = Vector3.down - Vector3.Dot(Vector3.down, up) * up;
            float m = dir.magnitude;
            dir = m > 0.01f ? dir / m : o.forward;
            return o.position + dir * WorldRadius;
        }

        void Spill(FluidType type, float ml)
        {
            FluidStream s = GetStream();
            s.SetOrigin(RimPoint());
            s.Add(type, ml);
            onOverflow.Invoke();
        }

        FluidStream GetStream()
        {
            if (stream == null) stream = FluidStream.Create(gameObject, this);
            return stream;
        }

        // ---------------------------------------------------------------- visuals

        void UpdateFillVisual()
        {
            if (!Application.isPlaying) return;
            bool show = showFill && !isDrain && total > 0f;
            if (!show)
            {
                if (fillRenderer != null) fillRenderer.enabled = false;
                return;
            }
            if (fillRenderer == null) fillRenderer = FluidVisuals.CreateCylinder("Fluid Fill", Opening);

            float height = FillFraction * depth * 0.98f;
            float diameter = openingRadius * 2f * 0.97f;
            Transform t = fillRenderer.transform;
            t.localPosition = new Vector3(0f, -depth + height * 0.5f, 0f);
            t.localScale = new Vector3(diameter, height * 0.5f, diameter);
            fillRenderer.enabled = true;
            FluidVisuals.Apply(fillRenderer, FluidMix.Blend(layers, out FluidType dominant), dominant);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Opening.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            const int Segments = 24;
            Vector3 prev = new Vector3(openingRadius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 p = new Vector3(Mathf.Cos(a) * openingRadius, 0f, Mathf.Sin(a) * openingRadius);
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawLine(prev + Vector3.down * depth, p + Vector3.down * depth);
                if (i % 6 == 0) Gizmos.DrawLine(p, p + Vector3.down * depth);
                prev = p;
            }
        }
    }
}
