using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    public class FluidContainer : MonoBehaviour
    {
        public static readonly List<FluidContainer> All = new List<FluidContainer>();

        [Header("Contents")]
        [Min(1f), Tooltip("Maximum volume in ml.")]
        public float capacity = 250f;
        [SerializeField, Tooltip("Current contents in ml, in the order each material was first poured in.")]
        protected List<FluidLayer> layers = new List<FluidLayer>();

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
        [Tooltip("Show the liquid inside the container. Its surface stays level when the container tilts.")]
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

        protected float total;
        protected float lastSubmergedTime = -10f;
        protected FluidStream stream;
        protected MeshRenderer fillRenderer;
        protected FluidFillMesh fillMesh;
        protected Vector3 fillGravity;
        protected bool fillDirty = true;

        // Lightweight steam/smoke cue for hot contents.
        protected ParticleSystem hotSmoke;
        protected Material hotSmokeMaterial;
        protected bool wasHot;

        public Transform Opening => opening != null ? opening : transform;
        public IReadOnlyList<FluidLayer> Layers => layers;
        public float Total => total;
        public float FillFraction => capacity > 0f ? Mathf.Clamp01(total / capacity) : 0f;
        public bool IsFull => total >= capacity - 0.01f;
        public bool IsEmpty => total <= 0f;
        public float WorldRadius => openingRadius * Opening.lossyScale.x;
        public float WorldDepth => depth * Opening.lossyScale.y;

        public float SurfaceWorldY => Opening.position.y - WorldDepth * (isDrain ? 1f : 1f - FillFraction);
        protected MeshCollider fillCollider;
        // for quality control
        public bool isRefrigerated = false;

        void Awake()
        {
            layers.RemoveAll(l => l.type == null || l.amount <= 0f);
            total = FluidMix.Total(layers);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDestroy()
        {
            if (fillMesh != null) Destroy(fillMesh.Mesh);
            if (hotSmoke != null) Destroy(hotSmoke.gameObject);
            if (hotSmokeMaterial != null) Destroy(hotSmokeMaterial);
        }

        public float GetAmount(FluidType type)
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].type == type) return layers[i].amount;
            return 0f;
        }

        public float GetFractionOfCapacity(FluidType type) => capacity > 0f ? GetAmount(type) / capacity : 0f;

        public float GetFractionOfContents(FluidType type) => total > 0f ? GetAmount(type) / total : 0f;

        public bool Accepts(FluidType type) =>
            type != null && (acceptedTypes == null || acceptedTypes.Count == 0 || acceptedTypes.Contains(type));

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

        public float Remove(float ml) => RemoveMix(ml, null);

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
            fillDirty = true;
        }

        public void MarkSubmerged() => lastSubmergedTime = Time.time;

        void Update()
        {
            if (total <= 0f) return;

            foreach (FluidLayer layer in layers)
            {
                layer.TickQuality(this, Time.deltaTime);
            }

            Vector3 gravity = Opening.InverseTransformDirection(Vector3.down);
            if ((gravity - fillGravity).sqrMagnitude > 1e-6f) fillDirty = true;

            if (!canPour || Time.time - lastSubmergedTime < 0.1f) return;
            float excess = total - MaxFillAtTilt(-gravity.y) * capacity;
            if (excess <= 0.01f) return;

            float rate = Mathf.Min(maxPourRate, Mathf.Max(excess * 6f, 15f));
            float amount = Mathf.Min(excess, rate * Time.deltaTime);
            FluidStream s = GetStream();
            s.SetOrigin(RimPoint());
            RemoveMix(amount, s);
        }

        void LateUpdate()
        {
            if (fillDirty)
            {
                fillDirty = false;
                UpdateFillVisual();
            }

            UpdateHotSmoke();
        }

        float MaxFillAtTilt(float cosTilt)
        {
            if (cosTilt >= 0.9999f) return 1f;
            if (cosTilt <= 0.02f) return 0f;
            float sin = Mathf.Sqrt(1f - cosTilt * cosTilt);
            return FluidFillMesh.FractionBelow(FluidFillMesh.RimHeight(openingRadius, sin), openingRadius, depth, sin, cosTilt);
        }

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

        bool ContainsHotFluid()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].isHot && layers[i].amount > 0.001f)
                    return true;
            }

            return false;
        }

        void UpdateHotSmoke()
        {
            bool hot = showFill && !isDrain && total > 0f && ContainsHotFluid();

            if (!hot)
            {
                if (hotSmoke != null && wasHot)
                    hotSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                wasHot = false;
                return;
            }

            if (hotSmoke == null)
            {
                var smokeObject = new GameObject("Hot Fluid Steam");
                // Keep the emitter aligned to world up even if the container tilts.
                // This prevents particles being emitted sideways or down into the liquid.
                smokeObject.transform.SetParent(null);
                smokeObject.transform.rotation = Quaternion.identity;
                hotSmoke = smokeObject.AddComponent<ParticleSystem>();

                var main = hotSmoke.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
                // Keep intrinsic speed near zero; upward movement is controlled below.
                main.startSpeed = 0f;
                // Do not let built-in particle gravity pull steam back into the cup.
                main.gravityModifier = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.78f, 0.82f, 0.86f, 0.28f),
                    new Color(0.95f, 0.96f, 0.98f, 0.48f));
                main.maxParticles = 40;

                var emission = hotSmoke.emission;
                emission.rateOverTime = 7f;

                // Slow, consistent world-up movement. No random horizontal velocity,
                // so wisps do not spin off in arbitrary directions.
                var velocity = hotSmoke.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                // Keep all three curves in Constant mode; mixing Constant and
                // Two Constants causes Unity's "curves must all be in the same mode" error.
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.012f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f);

                // Disable noise: it can add downward velocity and make wisps dip back
                // through the opening. With zero start speed/gravity and positive world-Y
                // velocity above, particles only travel upward.
                var noise = hotSmoke.noise;
                noise.enabled = false;

                var shape = hotSmoke.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Mathf.Max(0.001f, WorldRadius * 0.95f);
                // Emit throughout the circle area, not just near its center/edge.
                shape.radiusThickness = 1f;
                // The emitter itself is world-aligned, so this circle stays horizontal.
                shape.rotation = Vector3.zero;

                var colorOverLifetime = hotSmoke.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.82f, 0.86f, 0.9f), 0f),
                        new GradientColorKey(new Color(0.9f, 0.92f, 0.95f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.32f, 0.15f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colorOverLifetime.color = gradient;

                var renderer = smokeObject.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = 1f;

                // Particle systems need a compatible transparent particle shader.
                // Without an assigned material Unity may render them magenta.
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null)
                    particleShader = Shader.Find("Particles/Standard Unlit");
                if (particleShader == null)
                    particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

                if (particleShader != null)
                {
                    hotSmokeMaterial = new Material(particleShader)
                    {
                        name = "Hot Fluid Steam (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    renderer.sharedMaterial = hotSmokeMaterial;
                }
                else
                {
                    Debug.LogWarning(
                        "No compatible particle shader found for hot fluid steam. " +
                        "Assign a transparent particle material compatible with your render pipeline.",
                        this);
                }
            }

            // Keep the entire emission plane above the rim, not at the liquid surface.
            // Otherwise particles can begin inside the cup when it is partly filled or tilted.
            Transform o = Opening;
            Vector3 rimX = o.right * (openingRadius * o.lossyScale.x);
            Vector3 rimZ = o.forward * (openingRadius * o.lossyScale.z);
            float rimVerticalExtent = Mathf.Sqrt(rimX.y * rimX.y + rimZ.y * rimZ.y);
            float clearance = Mathf.Max(0.003f, WorldRadius * 0.12f);
            hotSmoke.transform.position = o.position + Vector3.up * (rimVerticalExtent + clearance);
            hotSmoke.transform.rotation = Quaternion.identity;

            var smokeShape = hotSmoke.shape;
            smokeShape.radius = Mathf.Max(0.001f, WorldRadius * 0.95f);
            smokeShape.radiusThickness = 1f;

            if (!hotSmoke.isPlaying)
                hotSmoke.Play();

            wasHot = true;
        }

        protected virtual void UpdateFillVisual()
        {
            if (fillRenderer == null)
            {
                fillMesh = new FluidFillMesh();

                fillRenderer = FluidVisuals.CreateRenderer(
                    "Fluid Fill",
                    Opening,
                    fillMesh.Mesh
                );

                fillCollider = fillRenderer.gameObject.AddComponent<MeshCollider>();
                fillCollider.convex = true;
                fillCollider.isTrigger = true;
                fillCollider.sharedMesh = fillMesh.Mesh;
            }
            
            if (!Application.isPlaying) return;
            Vector3 gravity = Opening.InverseTransformDirection(Vector3.down);
            fillGravity = gravity;
            float cos = -gravity.y;

            // Past roughly horizontal there is nothing left inside to show.
            bool show = showFill && !isDrain && total > 0f && cos > 0.03f;
            if (!show)
            {
                if (fillCollider != null) fillCollider.enabled = false;
                if (fillRenderer != null) fillRenderer.enabled = false;
                return;
            }
            if (fillRenderer == null)
            {
                fillMesh = new FluidFillMesh();
                fillRenderer = FluidVisuals.CreateRenderer("Fluid Fill", Opening, fillMesh.Mesh);
            }

            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            var lowSide = new Vector2(gravity.x, gravity.z);
            lowSide = sin > 0.0001f ? lowSide / lowSide.magnitude : Vector2.right;
            float height = FluidFillMesh.SolveHeight(FillFraction, openingRadius, depth, sin, cos);
            fillMesh.Rebuild(openingRadius * 0.97f, depth, height, sin, cos, lowSide);
            // Refresh the collider to match the newly generated liquid mesh.
            fillCollider.sharedMesh = null;
            fillCollider.sharedMesh = fillMesh.Mesh;
            fillCollider.enabled = true;

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
