using UnityEngine;

namespace MilkIt.Fluids
{
    public class FluidFillMesh
    {
        const int Sides = 20;
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
        public void Rebuild(
            float topRadius,
            float bottomRadius,
            float depth,
            float height,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            depth = Mathf.Max(depth, 0.0001f);
            topRadius = Mathf.Max(topRadius, 0f);
            bottomRadius = Mathf.Max(bottomRadius, 0f);
            cos = Mathf.Max(cos, 0.0001f);

            if (lowSide.sqrMagnitude > 0.000001f)
                lowSide.Normalize();
            else
                lowSide = Vector2.right;

            var surfaceNormal = new Vector3(
                -sin * lowSide.x,
                cos,
                -sin * lowSide.y
            ).normalized;

            vertices[SurfaceCenter] = new Vector3(
                0f,
                Mathf.Clamp(height / cos, -depth, 0f),
                0f
            );
            normals[SurfaceCenter] = surfaceNormal;

            vertices[CapCenter] = new Vector3(0f, -depth, 0f);
            normals[CapCenter] = Vector3.down;

            float slope = (topRadius - bottomRadius) / depth;

            for (int i = 0; i < Sides; i++)
            {
                float x = Cos[i];
                float z = Sin[i];

                float along = x * lowSide.x + z * lowSide.y;

                // Liquid surface height evaluated at each end of the funnel wall.
                float gapTop =
                    (height + sin * topRadius * along) / cos;

                float gapBottom =
                    (height + sin * bottomRadius * along) / cos + depth;

                // Find where the liquid surface intersects the tapered wall.
                float boundaryY;

                if (Mathf.Abs(gapTop) < 0.00001f)
                {
                    boundaryY = 0f;
                }
                else if (Mathf.Abs(gapBottom) < 0.00001f)
                {
                    boundaryY = -depth;
                }
                else if (gapTop > 0f && gapBottom > 0f)
                {
                    boundaryY = 0f;
                }
                else if (gapTop < 0f && gapBottom < 0f)
                {
                    boundaryY = -depth;
                }
                else
                {
                    float t = gapBottom / (gapBottom - gapTop);
                    boundaryY = Mathf.Lerp(-depth, 0f, t);
                }

                boundaryY = Mathf.Clamp(boundaryY, -depth, 0f);

                float boundaryRadius = RadiusAtY(
                    boundaryY,
                    topRadius,
                    bottomRadius,
                    depth
                );

                // Build concentric rings for the liquid surface.
                for (int k = 0; k < Rings; k++)
                {
                    float r = boundaryRadius * (k + 1f) / Rings;

                    float y = Mathf.Clamp(
                        (height + sin * r * along) / cos,
                        -depth,
                        0f
                    );

                    int index = SurfaceRings + k * Sides + i;

                    vertices[index] = new Vector3(x * r, y, z * r);
                    normals[index] = surfaceNormal;
                }

                // Tapered outer wall.
                vertices[SideBottom + i] =
                    new Vector3(x * bottomRadius, -depth, z * bottomRadius);

                vertices[SideTop + i] =
                    new Vector3(x * boundaryRadius, boundaryY, z * boundaryRadius);

                Vector3 sideNormal = new Vector3(x, -slope, z).normalized;

                normals[SideBottom + i] = sideNormal;
                normals[SideTop + i] = sideNormal;

                // Bottom cap.
                vertices[CapRing + i] =
                    new Vector3(x * bottomRadius, -depth, z * bottomRadius);

                normals[CapRing + i] = Vector3.down;
            }

            float maxRadius = Mathf.Max(topRadius, bottomRadius);

            Mesh.vertices = vertices;
            Mesh.normals = normals;
            Mesh.bounds = new Bounds(
                new Vector3(0f, -depth * 0.5f, 0f),
                new Vector3(maxRadius * 2f, depth, maxRadius * 2f)
            );
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

        public static float FractionBelow(
            float height,
            float topRadius,
            float bottomRadius,
            float depth,
            float sin,
            float cos)
        {
            depth = Mathf.Max(depth, 0.0001f);
            topRadius = Mathf.Max(topRadius, 0f);
            bottomRadius = Mathf.Max(bottomRadius, 0f);
            cos = Mathf.Max(cos, 0.0001f);
            sin = Mathf.Max(sin, 0f);

            float radiusSum =
                bottomRadius * bottomRadius +
                bottomRadius * topRadius +
                topRadius * topRadius;

            if (radiusSum < 0.000001f)
                return 0f;

            // Upright funnel: use the exact frustum volume formula.
            if (sin < 0.0001f)
            {
                float fillDepth = Mathf.Clamp(
                    height / cos + depth,
                    0f,
                    depth
                );

                float t = fillDepth / depth;
                float delta = topRadius - bottomRadius;

                float partialVolume =
                    bottomRadius * bottomRadius * t +
                    bottomRadius * delta * t * t +
                    delta * delta * t * t * t / 3f;

                return Mathf.Clamp01(partialVolume / (radiusSum / 3f));
            }

            // Tilted funnel: integrate submerged circular slices.
            float sum = 0f;
            float step = depth / Samples;

            for (int i = 0; i < Samples; i++)
            {
                float y = -depth + (i + 0.5f) * step;

                float radius = RadiusAtY(
                    y,
                    topRadius,
                    bottomRadius,
                    depth
                );

                if (radius <= 0.000001f)
                    continue;

                float threshold = (cos * y - height) / sin;
                float q = threshold / radius;

                float sliceFraction;

                if (q <= -1f)
                    sliceFraction = 1f;
                else if (q >= 1f)
                    sliceFraction = 0f;
                else
                {
                    float root = Mathf.Sqrt(Mathf.Max(0f, 1f - q * q));

                    sliceFraction =
                        (Mathf.Acos(q) - q * root) / Mathf.PI;
                }

                sum += radius * radius * sliceFraction;
            }

            return Mathf.Clamp01(3f * sum / (Samples * radiusSum));
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

        public static float RimHeight(
            float topRadius,
            float bottomRadius,
            float sin)
        {
            // The opening rim is defined by topRadius.
            return RimHeight(topRadius, sin);
        }
        
        public static float SolveHeight(
            float fraction,
            float topRadius,
            float bottomRadius,
            float depth,
            float sin,
            float cos)
        {
            fraction = Mathf.Clamp01(fraction);
            depth = Mathf.Max(depth, 0.0001f);
            cos = Mathf.Max(cos, 0.0001f);
        
            float hi = RimHeight(topRadius, bottomRadius, sin);
        
            float fractionAtRim = FractionBelow(
                hi,
                topRadius,
                bottomRadius,
                depth,
                sin,
                cos
            );
        
            if (fraction >= fractionAtRim)
                return hi;
        
            float lo = Mathf.Min(
                hi,
                -cos * depth - sin * bottomRadius
            );
        
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
        
                float currentFraction = FractionBelow(
                    mid,
                    topRadius,
                    bottomRadius,
                    depth,
                    sin,
                    cos
                );
        
                if (currentFraction < fraction)
                    lo = mid;
                else
                    hi = mid;
            }
        
            return (lo + hi) * 0.5f;
        }
        static float RadiusAtY(
            float y,
            float topRadius,
            float bottomRadius,
            float depth)
        {
            float t = Mathf.Clamp01((y + depth) / Mathf.Max(depth, 0.0001f));
            return Mathf.Lerp(bottomRadius, topRadius, t);
        }
    }
}
