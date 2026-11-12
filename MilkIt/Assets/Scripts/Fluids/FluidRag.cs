using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    /// <summary>
    /// A cloth that cleans spills. Touching a puddle is not enough: it has to be rubbed back and forth
    /// over it for 'scrubTime' seconds in total. Put it on any grabbable object.
    /// </summary>
    public class FluidRag : MonoBehaviour
    {
        [Tooltip("Seconds of rubbing needed to clean one puddle.")]
        public float scrubTime = 3f;
        [Tooltip("How far around the rag's centre it wipes (m).")]
        public float radius = 0.1f;
        [Tooltip("How far above or below the puddle's surface the rag may be and still count as touching (m).")]
        public float reach = 0.08f;
        [Tooltip("m/s along the surface. Slower than this does not count as rubbing.")]
        public float minRubSpeed = 0.05f;

        public UnityEvent<FluidPuddle> onPuddleCleaned = new UnityEvent<FluidPuddle>();

        Vector3 lastPosition;

        void OnEnable() => lastPosition = transform.position;

        void Update()
        {
            Vector3 position = transform.position;
            Vector3 move = position - lastPosition;
            lastPosition = position;

            float dt = Time.deltaTime;
            FluidSpillManager manager = FluidSpillManager.Existing;
            if (manager == null || dt <= 0f || scrubTime <= 0f) return;

            IReadOnlyList<FluidPuddle> puddles = manager.Puddles;
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                FluidPuddle puddle = puddles[i];
                Transform t = puddle.transform;
                Vector3 normal = t.up;
                Vector3 offset = position - t.position;

                float height = Vector3.Dot(offset, normal);
                if (Mathf.Abs(height) > reach) continue;
                float touch = radius + puddle.Radius;
                if ((offset - normal * height).sqrMagnitude > touch * touch) continue;

                // Only movement along the surface counts as rubbing.
                Vector3 slide = move - normal * Vector3.Dot(move, normal);
                if (slide.magnitude < minRubSpeed * dt) continue;

                if (puddle.Scrub(dt / scrubTime)) onPuddleCleaned.Invoke(puddle);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
