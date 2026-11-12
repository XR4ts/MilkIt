using UnityEngine;

namespace MilkIt.Fluids
{
    internal sealed class FluidFillMesh
    {
        const int Sides = 24;
        const int Rings = 5;
        const int Samples = 32;

        const int SurfaceCenter = 0;
        const int SurfaceRings = 1;
        const int SideBottom = SurfaceRings + Rings * Sides;
        const int SideTop = SideBottom + Sides;
        const int CapCenter = SideTop + Sides;
        const int CapRing = CapCenter + 1;
        const int VertexCount = CapRing + Sides;

        static readonly float[] Cos = new float[Sides];
        static readonly float[] Sin = new float[Sides];

        readonly Vector3[] vertices = new Vector3[VertexCount];
        readonly Vector3[] normals = new Vector3[VertexCount];

        public Mesh Mesh { get; }

        static FluidFillMesh()
        {
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                Cos[i] = Mathf.Cos(a);
                Sin[i] = Mathf.Sin(a);
            }
        }

        public FluidFillMesh()
        {
            Mesh = new Mesh { name = "Fluid Fill", hideFlags = HideFlags.HideAndDontSave };
            Mesh.MarkDynamic();

            var triangles = new int[Sides * 3 + (Rings - 1) * Sides * 6 + Sides * 6 + Sides * 3];
            int t = 0;
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;

                triangles[t++] = SurfaceCenter;
                triangles[t++] = SurfaceRings + j;
                triangles[t++] = SurfaceRings + i;
                for (int k = 0; k < Rings - 1; k++)
                {
                    int inner = SurfaceRings + k * Sides, outer = inner + Sides;
                    triangles[t++] = inner + i; triangles[t++] = outer + j; triangles[t++] = outer + i;
                    triangles[t++] = inner + i; triangles[t++] = inner + j; triangles[t++] = outer + j;
                }

                triangles[t++] = SideBottom + i; triangles[t++] = SideTop + i; triangles[t++] = SideTop + j;
                triangles[t++] = SideBottom + i; triangles[t++] = SideTop + j; triangles[t++] = SideBottom + j;

                triangles[t++] = CapCenter; triangles[t++] = CapRing + i; triangles[t++] = CapRing + j;

                normals[SideBottom + i] = normals[SideTop + i] = new Vector3(Cos[i], 0f, Sin[i]);
                normals[CapRing + i] = Vector3.down;
            }
            normals[CapCenter] = Vector3.down;

            Mesh.vertices = vertices;
            Mesh.normals = normals;
            Mesh.triangles = triangles;
        }

        public void Rebuild(float radius, float depth, float height, float sin, float cos, Vector2 lowSide)
        {
            var surfaceNormal = new Vector3(-sin * lowSide.x, cos, -sin * lowSide.y);
            vertices[SurfaceCenter] = new Vector3(0f, Mathf.Clamp(height / cos, -depth, 0f), 0f);
            normals[SurfaceCenter] = surfaceNormal;
            vertices[CapCenter] = new Vector3(0f, -depth, 0f);

            for (int i = 0; i < Sides; i++)
            {
                float along = Cos[i] * lowSide.x + Sin[i] * lowSide.y;
                float y = 0f;
                for (int k = 0; k < Rings; k++)
                {
                    float r = radius * (k + 1) / Rings;
                    y = Mathf.Clamp((height + sin * r * along) / cos, -depth, 0f);
                    vertices[SurfaceRings + k * Sides + i] = new Vector3(Cos[i] * r, y, Sin[i] * r);
                    normals[SurfaceRings + k * Sides + i] = surfaceNormal;
                }
                var bottom = new Vector3(Cos[i] * radius, -depth, Sin[i] * radius);
                vertices[SideBottom + i] = bottom;
                vertices[CapRing + i] = bottom;
                vertices[SideTop + i] = new Vector3(bottom.x, y, bottom.z);
            }

            Mesh.vertices = vertices;
            Mesh.normals = normals;
            Mesh.bounds = new Bounds(new Vector3(0f, -depth * 0.5f, 0f), new Vector3(radius * 2f, depth, radius * 2f));
        }

        public static float FractionBelow(float height, float radius, float depth, float sin, float cos)
        {
            // Substituting u = radius * sin(phi) keeps the integrand smooth, so few samples are needed.
            float sum = 0f;
            const float Step = Mathf.PI / Samples;
            for (int i = 0; i < Samples; i++)
            {
                float phi = -Mathf.PI * 0.5f + (i + 0.5f) * Step;
                float cosPhi = Mathf.Cos(phi);
                float column = Mathf.Clamp((height + sin * radius * Mathf.Sin(phi)) / cos + depth, 0f, depth);
                sum += column * cosPhi * cosPhi;
            }
            return Mathf.Clamp01(sum * Step * 2f / (Mathf.PI * depth));
        }

        public static float RimHeight(float radius, float sin) => -sin * radius;

        public static float SolveHeight(float fraction, float radius, float depth, float sin, float cos)
        {
            float hi = RimHeight(radius, sin);
            if (fraction >= FractionBelow(hi, radius, depth, sin, cos)) return hi;
            float lo = hi - cos * depth - 2f * sin * radius;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (FractionBelow(mid, radius, depth, sin, cos) < fraction) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }
    }
}
