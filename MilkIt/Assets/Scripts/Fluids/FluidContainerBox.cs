
using UnityEngine;

namespace MilkIt.Fluids
{
    public class FluidContainerBox : FluidContainer
    {
        [Header("Rectangular Container")]
        [SerializeField, Min(0.001f)]
        float width = 0.2f;

        [SerializeField, Min(0.001f)]
        float length = 0.15f;

        RectFluidFillMesh rectFillMesh;
        Renderer rectFillRenderer;

        protected override void UpdateFillVisual()
        {
            if (!Application.isPlaying)
                return;

            if (Opening == null)
                return;

            Vector3 gravity =
                Opening.InverseTransformDirection(Vector3.down);

            fillGravity = gravity;

            float cos = -gravity.y;

            bool show =
                showFill &&
                !isDrain &&
                total > 0f &&
                cos > 0.03f;

            if (!show)
            {
                if (rectFillRenderer != null)
                    rectFillRenderer.enabled = false;

                return;
            }

            if (rectFillRenderer == null)
            {
                rectFillMesh = new RectFluidFillMesh();

                rectFillRenderer = FluidVisuals.CreateRenderer(
                    "Rectangular Fluid Fill",
                    Opening,
                    rectFillMesh.Mesh
                );
            }

            float sin = Mathf.Sqrt(
                Mathf.Max(0f, 1f - cos * cos));

            Vector2 lowSide = new Vector2(
                gravity.x,
                gravity.z
            );

            if (sin > 0.0001f &&
                lowSide.sqrMagnitude > 0.000001f)
            {
                lowSide.Normalize();
            }
            else
            {
                lowSide = Vector2.right;
            }

            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            float height = RectFluidFillMesh.SolveHeight(
                FillFraction,
                halfWidth,
                halfLength,
                depth,
                sin,
                cos,
                lowSide
            );

            // Slightly inset the visual so it does not intersect the
            // vessel's inside walls.
            rectFillMesh.Rebuild(
                halfWidth * 0.97f,
                halfLength * 0.97f,
                depth,
                height,
                sin,
                cos,
                lowSide
            );

            rectFillRenderer.enabled = true;

            FluidVisuals.Apply(
                rectFillRenderer,
                FluidMix.Blend(layers, out FluidType dominant),
                dominant
            );
        }

        void OnDrawGizmosSelected()
        {
            if (Opening == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = Opening.localToWorldMatrix;
            Gizmos.color = Color.cyan;

            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            Vector3[] top =
            {
                new Vector3(-halfWidth, 0f, -halfLength),
                new Vector3( halfWidth, 0f, -halfLength),
                new Vector3( halfWidth, 0f,  halfLength),
                new Vector3(-halfWidth, 0f,  halfLength)
            };

            Vector3[] bottom =
            {
                new Vector3(-halfWidth, -depth, -halfLength),
                new Vector3( halfWidth, -depth, -halfLength),
                new Vector3( halfWidth, -depth,  halfLength),
                new Vector3(-halfWidth, -depth,  halfLength)
            };

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;

                Gizmos.DrawLine(top[i], top[next]);
                Gizmos.DrawLine(bottom[i], bottom[next]);
                Gizmos.DrawLine(top[i], bottom[i]);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        protected virtual void OnDestroy()
        {
            if (rectFillRenderer != null)
            {
                if (Application.isPlaying)
                    Destroy(rectFillRenderer.gameObject);
                else
                    DestroyImmediate(rectFillRenderer.gameObject);
            }

            if (rectFillMesh != null && rectFillMesh.Mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(rectFillMesh.Mesh);
                else
                    DestroyImmediate(rectFillMesh.Mesh);
            }
        }
    }
}
