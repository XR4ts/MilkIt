using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    /// <summary>
    /// Owns every puddle. One per scene; created automatically with default settings if missing.
    /// Puddles fade after 'lifetime' seconds. Turn 'fadeEnabled' off (or call SetFadeEnabled / ToggleFade) to keep
    /// every puddle until it is cleaned with a FluidRag, CleanAt, CleanAll or FluidPuddle.CleanUp.
    /// </summary>
    public class FluidSpillManager : MonoBehaviour
    {
        [Tooltip("The fade timer for all puddles. Off = puddles stay until cleaned up.")]
        public bool fadeEnabled = true;
        [Tooltip("Seconds a puddle stays after the last drop landed on it.")]
        public float lifetime = 10f;
        public float fadeDuration = 3f;
        [Min(1), Tooltip("Oldest fading puddle is reused beyond this.")]
        public int maxPuddles = 24;
        [Tooltip("A spill this close to an existing puddle joins it (m).")]
        public float mergeDistance = 0.12f;
        public float maxRadius = 0.5f;
        [Tooltip("m/s the puddle edge spreads.")]
        public float spreadSpeed = 0.25f;
        [Tooltip("Puddle height in metres; with the volume it sets how wide a puddle gets.")]
        public float liquidThickness = 0.002f;
        public float granularThickness = 0.012f;

        public UnityEvent<FluidPuddle> onPuddleCreated = new UnityEvent<FluidPuddle>();
        [Tooltip("A puddle faded away or was cleaned up.")]
        public UnityEvent<FluidPuddle> onPuddleRemoved = new UnityEvent<FluidPuddle>();
        public UnityEvent<FluidPuddle> onPuddleCleaned = new UnityEvent<FluidPuddle>();

        static FluidSpillManager instance;
        readonly List<FluidPuddle> active = new List<FluidPuddle>();
        readonly Stack<FluidPuddle> pool = new Stack<FluidPuddle>();

        public IReadOnlyList<FluidPuddle> Puddles => active;

        /// <summary>The manager if there is one, without creating it.</summary>
        public static FluidSpillManager Existing => instance;

        public static FluidSpillManager Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<FluidSpillManager>();
                if (instance == null) instance = new GameObject("Fluid Spill Manager").AddComponent<FluidSpillManager>();
                return instance;
            }
        }

        void Awake()
        {
            if (instance == null) instance = this;
        }

        /// <summary>Turns the fade timer for all puddles on or off. Timers pause while off.</summary>
        public void SetFadeEnabled(bool enabled) => fadeEnabled = enabled;

        public void ToggleFade() => fadeEnabled = !fadeEnabled;

        public void AddSpill(Vector3 point, Vector3 normal, FluidType type, float ml)
        {
            if (type == null || ml <= 0f) return;
            FluidPuddle puddle = FindNear(point);
            if (puddle == null) puddle = Spawn(point, normal);
            puddle.Add(type, ml);
        }

        /// <summary>Cleans every puddle touching a sphere, e.g. around a mop head. Returns how many were cleaned.</summary>
        public int CleanAt(Vector3 position, float radius)
        {
            int cleaned = 0;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                FluidPuddle p = active[i];
                float reach = radius + p.Radius;
                if ((p.transform.position - position).sqrMagnitude > reach * reach) continue;
                Remove(p, true);
                cleaned++;
            }
            return cleaned;
        }

        public void CleanAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Remove(active[i], true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
                if (!active[i].Tick(dt, fadeEnabled)) Remove(active[i], false);
        }

        FluidPuddle FindNear(Vector3 point)
        {
            FluidPuddle best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < active.Count; i++)
            {
                FluidPuddle p = active[i];
                float reach = Mathf.Max(mergeDistance, p.Radius);
                float sqr = (p.transform.position - point).sqrMagnitude;
                if (sqr > reach * reach || sqr >= bestSqr) continue;
                best = p;
                bestSqr = sqr;
            }
            return best;
        }

        FluidPuddle Spawn(Vector3 point, Vector3 normal)
        {
            if (active.Count >= maxPuddles)
            {
                // Reuse the oldest puddle that is allowed to disappear; if none may (fade off or all permanent), grow the nearest one.
                FluidPuddle oldest = null;
                for (int i = 0; i < active.Count && oldest == null; i++)
                    if (fadeEnabled && !active[i].permanent) oldest = active[i];
                if (oldest == null) return Nearest(point);
                Remove(oldest, false);
            }

            FluidPuddle puddle = pool.Count > 0 ? pool.Pop() : new GameObject("Fluid Puddle").AddComponent<FluidPuddle>();
            puddle.transform.SetParent(transform, true);
            puddle.Init(this, point, normal);
            active.Add(puddle);
            onPuddleCreated.Invoke(puddle);
            return puddle;
        }

        FluidPuddle Nearest(Vector3 point)
        {
            FluidPuddle best = active[0];
            for (int i = 1; i < active.Count; i++)
                if ((active[i].transform.position - point).sqrMagnitude < (best.transform.position - point).sqrMagnitude)
                    best = active[i];
            return best;
        }

        internal void Remove(FluidPuddle puddle, bool cleaned)
        {
            if (!active.Remove(puddle)) return;
            if (cleaned)
            {
                puddle.onCleaned.Invoke();
                onPuddleCleaned.Invoke(puddle);
            }
            onPuddleRemoved.Invoke(puddle);
            puddle.gameObject.SetActive(false);
            pool.Push(puddle);
        }
    }
}
