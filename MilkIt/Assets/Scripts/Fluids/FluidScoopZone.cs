using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.Fluids
{
    /// <summary>
    /// Makes a source scoopable: a box of material (bag of coffee grains, full sink) that fills any
    /// scoopable container whose opening is dipped inside it. Infinite.
    /// </summary>
    public class FluidScoopZone : MonoBehaviour
    {
        public FluidType fluidType;
        [Tooltip("ml per second while a container's opening is inside.")]
        public float fillRate = 400f;
        [Tooltip("Box in local space. Its top face is the surface of the material.")]
        public Vector3 center;
        public Vector3 size = new Vector3(0.2f, 0.2f, 0.2f);

        void Update()
        {
            if (fluidType == null) return;
            List<FluidContainer> all = FluidContainer.All;
            Vector3 half = size * 0.5f;
            for (int i = 0; i < all.Count; i++)
            {
                FluidContainer c = all[i];
                if (!c.scoopable || c.isDrain) continue;
                Vector3 p = transform.InverseTransformPoint(c.Opening.position) - center;
                if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > half.y || Mathf.Abs(p.z) > half.z) continue;
                c.MarkSubmerged();
                if (c.Accepts(fluidType)) c.Add(fluidType, fillRate * Time.deltaTime);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = fluidType != null ? fluidType.color : Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
