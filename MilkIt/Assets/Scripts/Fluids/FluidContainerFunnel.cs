using UnityEngine;

namespace MilkIt.Fluids{
    public class FluidContainerFunnel : FluidContainer
    {
        [Header("Funnel shape (treated as a funnel, from topRadius to bottomRadius)")]
        [Tooltip("Centre of the rim, +Y pointing out of the container. Defaults to this transform.")]
        public float topRadius = 0.07f;
        public float bottomRadius = 0.035f;

        protected override void UpdateFillVisual()
        {
            if (!Application.isPlaying) return;

            Vector3 gravity = Opening.InverseTransformDirection(Vector3.down);
            fillGravity = gravity;
            float cos = -gravity.y;

            // Past roughly horizontal there is nothing left inside to show.
            bool show = showFill && !isDrain && total > 0f && cos > 0.03f;
            if (!show)
            {
                if (fillRenderer != null)
                    fillRenderer.enabled = false;

                return;
            }

            if (fillRenderer == null)
            {
                fillMesh = new FluidFillMesh();
                fillRenderer = FluidVisuals.CreateRenderer(
                    "Fluid Fill",
                    Opening,
                    fillMesh.Mesh
                );
            }

            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));

            var lowSide = new Vector2(gravity.x, gravity.z);
            lowSide = sin > 0.0001f
                ? lowSide / lowSide.magnitude
                : Vector2.right;

            // Solve the fill height using the tapered funnel dimensions.
            float height = FluidFillMesh.SolveHeight(
                FillFraction,
                topRadius,
                bottomRadius,
                depth,
                sin,
                cos
            );

            // Build the fluid mesh inside the tapered walls.
            fillMesh.Rebuild(
                topRadius * 0.97f,
                bottomRadius * 0.97f,
                depth,
                height,
                sin,
                cos,
                lowSide
            );

            fillRenderer.enabled = true;

            FluidVisuals.Apply(
                fillRenderer,
                FluidMix.Blend(layers, out FluidType dominant),
                dominant
            );
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Opening.localToWorldMatrix;
            Gizmos.color = Color.cyan;

            const int Segments = 24;

            Vector3 previousTop = new Vector3(topRadius, 0f, 0f);
            Vector3 previousBottom = new Vector3(bottomRadius, -depth, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);

                Vector3 top = new Vector3(x * topRadius, 0f, z * topRadius);
                Vector3 bottom = new Vector3(x * bottomRadius, -depth, z * bottomRadius);

                // Top and bottom rings.
                Gizmos.DrawLine(previousTop, top);
                Gizmos.DrawLine(previousBottom, bottom);

                // Connect the rings to form the tapered funnel walls.
                if (i % 6 == 0)
                    Gizmos.DrawLine(top, bottom);

                previousTop = top;
                previousBottom = bottom;
            }
        }
    }
}