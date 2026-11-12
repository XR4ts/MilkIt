using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilkIt.Fluids
{
    public class FluidStream : MonoBehaviour
    {
        [Tooltip("Container this stream comes out of; never chosen as its own target.")]
        public FluidContainer owner;
        [Tooltip("What the stream can land on. Movable objects (rigidbodies, the player rig, containers) and steep surfaces are always skipped: fluid runs off them to the fixed surface below.")]
        public LayerMask obstacles = ~0;
        public float maxDistance = 5f;
        [Tooltip("m/s. Only affects thickness: thickness comes from flow rate divided by this speed.")]
        public float flowSpeed = 0.8f;
        [Tooltip("m/s. How fast the stream's tip drops when a pour starts.")]
        public float fallSpeed = 4f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        readonly List<FluidLayer> pending = new List<FluidLayer>();
        Vector3 origin;
        MeshRenderer visual;
        float smoothedRate;
        float visibleLength;

        public bool IsPouring { get; private set; }

        public static FluidStream Create(GameObject host, FluidContainer owner)
        {
            var s = host.AddComponent<FluidStream>();
            s.owner = owner;
            return s;
        }

        public void SetOrigin(Vector3 worldPosition) => origin = worldPosition;

        public void Add(FluidType type, float ml)
        {
            if (type == null || ml <= 0f) return;
            FluidMix.Add(pending, type, ml);
        }

        void LateUpdate()
        {
            float total = FluidMix.Total(pending);
            if (total <= 0f)
            {
                if (IsPouring)
                {
                    IsPouring = false;
                    visibleLength = 0f;
                    if (visual != null) visual.enabled = false;
                }
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float rate = total / dt;
            smoothedRate = IsPouring ? Mathf.Lerp(smoothedRate, rate, 1f - Mathf.Exp(-12f * dt)) : rate;
            IsPouring = true;

            Color color = FluidMix.Blend(pending, out FluidType dominant);

            FluidContainer target = FindReceiver();
            bool hasHit = FindObstacle(out RaycastHit hit);
            float endY;
            if (target != null && (!hasHit || hit.distance >= origin.y - target.Opening.position.y - 0.005f))
            {
                endY = Mathf.Min(target.SurfaceWorldY, origin.y);
                for (int i = 0; i < pending.Count; i++) target.Receive(pending[i].type, pending[i].amount);
            }
            else if (hasHit)
            {
                endY = hit.point.y;
                FluidSpillManager spills = FluidSpillManager.Instance;
                for (int i = 0; i < pending.Count; i++) spills.AddSpill(hit.point, hit.normal, pending[i].type, pending[i].amount);
            }
            else
            {
                endY = origin.y - maxDistance;
            }
            pending.Clear();

            UpdateVisual(origin.y - endY, color, dominant);
        }

        FluidContainer FindReceiver()
        {
            FluidContainer best = null;
            float bestY = float.NegativeInfinity;
            List<FluidContainer> all = FluidContainer.All;
            for (int i = 0; i < all.Count; i++)
            {
                FluidContainer c = all[i];
                if (c == owner || !c.canReceive) continue;
                Transform o = c.Opening;
                if (o.up.y < 0.35f) continue;
                Vector3 p = o.position;
                if (p.y > origin.y + 0.01f || p.y <= bestY) continue;
                float dx = p.x - origin.x, dz = p.z - origin.z;
                float r = c.WorldRadius;
                if (dx * dx + dz * dz > r * r) continue;
                best = c;
                bestY = p.y;
            }
            return best;
        }

        bool FindObstacle(out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, maxDistance, obstacles, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].collider.transform;
                if (t.IsChildOf(transform)) continue;
                if (!CanSettleOn(Hits[i])) continue;
                if (found && Hits[i].distance >= nearest.distance) continue;
                nearest = Hits[i];
                found = true;
            }
            return found;
        }

        static bool CanSettleOn(RaycastHit hit)
        {
            Collider c = hit.collider;
            if (hit.normal.y < 0.5f || c.attachedRigidbody != null) return false;
            return c.GetComponentInParent<FluidContainer>() == null && c.GetComponentInParent<XROrigin>() == null;
        }

        void UpdateVisual(float length, Color color, FluidType dominant)
        {
            if (visual == null) visual = FluidVisuals.CreateCylinder("Fluid Stream", null);

            length = Mathf.Max(length, 0f);
            visibleLength = Mathf.Min(length, visibleLength + fallSpeed * Time.deltaTime);

            float area = smoothedRate * 1e-6f / Mathf.Max(flowSpeed, 0.01f);
            float diameter = 2f * Mathf.Sqrt(area / Mathf.PI);
            if (dominant != null && dominant.IsGranular) diameter *= 1.5f;
            diameter = Mathf.Clamp(diameter, 0.003f, 0.1f);

            Transform t = visual.transform;
            t.SetPositionAndRotation(origin + Vector3.down * (visibleLength * 0.5f), Quaternion.identity);
            t.localScale = new Vector3(diameter, visibleLength * 0.5f, diameter);
            visual.enabled = visibleLength > 0.001f;
            FluidVisuals.Apply(visual, color, dominant);
        }

        void OnDisable()
        {
            pending.Clear();
            IsPouring = false;
            visibleLength = 0f;
            if (visual != null) visual.enabled = false;
        }

        void OnDestroy()
        {
            if (visual != null) Destroy(visual.gameObject);
        }
    }
}
