
using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.Fluids
{
    internal sealed class RectFluidFillMesh
    {
        const int Samples = 16;
        const float Epsilon = 0.000001f;

        static readonly int[][] Faces =
        {
            new[] { 0, 1, 2, 3 }, // Bottom
            new[] { 4, 7, 6, 5 }, // Top
            new[] { 0, 4, 5, 1 }, // Front
            new[] { 3, 2, 6, 7 }, // Back
            new[] { 0, 3, 7, 4 }, // Left
            new[] { 1, 5, 6, 2 }  // Right
        };

        static readonly Vector3[] FaceNormals =
        {
            Vector3.down,
            Vector3.up,
            Vector3.back,
            Vector3.forward,
            Vector3.left,
            Vector3.right
        };

        static readonly int[,] Edges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        readonly Vector3[] corners = new Vector3[8];
        readonly float[] distances = new float[8];

        readonly List<Vector3> vertices = new List<Vector3>(64);
        readonly List<Vector3> normals = new List<Vector3>(64);
        readonly List<int> triangles = new List<int>(96);

        readonly List<Vector3> clipInput = new List<Vector3>(8);
        readonly List<Vector3> clipOutput = new List<Vector3>(8);
        readonly List<Vector3> cutPoints = new List<Vector3>(8);

        readonly PointComparer pointComparer = new PointComparer();

        public Mesh Mesh { get; }

        sealed class PointComparer : IComparer<Vector3>
        {
            public Vector3 Center;
            public Vector3 U;
            public Vector3 V;

            public int Compare(Vector3 a, Vector3 b)
            {
                Vector3 da = a - Center;
                Vector3 db = b - Center;

                float aa = Mathf.Atan2(
                    Vector3.Dot(da, V),
                    Vector3.Dot(da, U));

                float ab = Mathf.Atan2(
                    Vector3.Dot(db, V),
                    Vector3.Dot(db, U));

                return aa.CompareTo(ab);
            }
        }

        public RectFluidFillMesh()
        {
            Mesh = new Mesh
            {
                name = "Rectangular Fluid Fill",
                hideFlags = HideFlags.HideAndDontSave
            };

            Mesh.MarkDynamic();
        }

        static float PlaneValue(
            Vector3 point,
            float height,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            return cos * point.y
                 - sin * (point.x * lowSide.x + point.z * lowSide.y)
                 - height;
        }

        static void AddUnique(List<Vector3> points, Vector3 point)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - point).sqrMagnitude < 0.0000001f)
                    return;
            }

            points.Add(point);
        }

        void ClipFace(
            float height,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            clipOutput.Clear();

            if (clipInput.Count == 0)
                return;

            Vector3 previous = clipInput[clipInput.Count - 1];

            float previousDistance = PlaneValue(
                previous, height, sin, cos, lowSide);

            bool previousInside = previousDistance <= Epsilon;

            for (int i = 0; i < clipInput.Count; i++)
            {
                Vector3 current = clipInput[i];

                float currentDistance = PlaneValue(
                    current, height, sin, cos, lowSide);

                bool currentInside = currentDistance <= Epsilon;

                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;

                    if (Mathf.Abs(denominator) > Epsilon)
                    {
                        float t = Mathf.Clamp01(
                            previousDistance / denominator);

                        AddUnique(
                            clipOutput,
                            Vector3.Lerp(previous, current, t));
                    }
                }

                if (currentInside)
                    AddUnique(clipOutput, current);

                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            // Remove a repeated closing vertex, if present.
            if (clipOutput.Count > 1 &&
                (clipOutput[0] - clipOutput[clipOutput.Count - 1])
                    .sqrMagnitude < 0.0000001f)
            {
                clipOutput.RemoveAt(clipOutput.Count - 1);
            }
        }

        void AddPolygon(List<Vector3> polygon, Vector3 normal)
        {
            if (polygon.Count < 3)
                return;

            int start = vertices.Count;

            for (int i = 0; i < polygon.Count; i++)
            {
                vertices.Add(polygon[i]);
                normals.Add(normal);
            }

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i + 1);
            }
        }

        public void Rebuild(
            float halfWidth,
            float halfLength,
            float depth,
            float height,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            halfWidth = Mathf.Max(halfWidth, 0.0001f);
            halfLength = Mathf.Max(halfLength, 0.0001f);
            depth = Mathf.Max(depth, 0.0001f);
            cos = Mathf.Max(cos, 0.0001f);

            if (lowSide.sqrMagnitude > Epsilon)
                lowSide.Normalize();
            else
                lowSide = Vector2.right;

            // Box corners: 0-3 bottom, 4-7 top.
            corners[0] = new Vector3(-halfWidth, -depth, -halfLength);
            corners[1] = new Vector3( halfWidth, -depth, -halfLength);
            corners[2] = new Vector3( halfWidth, -depth,  halfLength);
            corners[3] = new Vector3(-halfWidth, -depth,  halfLength);

            corners[4] = new Vector3(-halfWidth, 0f, -halfLength);
            corners[5] = new Vector3( halfWidth, 0f, -halfLength);
            corners[6] = new Vector3( halfWidth, 0f,  halfLength);
            corners[7] = new Vector3(-halfWidth, 0f,  halfLength);

            vertices.Clear();
            normals.Clear();
            triangles.Clear();
            cutPoints.Clear();

            for (int i = 0; i < corners.Length; i++)
            {
                distances[i] = PlaneValue(
                    corners[i], height, sin, cos, lowSide);

                if (Mathf.Abs(distances[i]) <= Epsilon)
                    AddUnique(cutPoints, corners[i]);
            }

            // Find the intersections of the liquid plane with box edges.
            for (int i = 0; i < Edges.GetLength(0); i++)
            {
                int a = Edges[i, 0];
                int b = Edges[i, 1];

                float da = distances[a];
                float db = distances[b];

                if ((da < -Epsilon && db > Epsilon) ||
                    (da > Epsilon && db < -Epsilon))
                {
                    float t = da / (da - db);

                    AddUnique(
                        cutPoints,
                        Vector3.Lerp(corners[a], corners[b], t));
                }
            }

            // Clip each outer box face to the volume below the liquid plane.
            for (int f = 0; f < Faces.Length; f++)
            {
                clipInput.Clear();

                int[] face = Faces[f];

                for (int i = 0; i < face.Length; i++)
                    clipInput.Add(corners[face[i]]);

                ClipFace(height, sin, cos, lowSide);
                AddPolygon(clipOutput, FaceNormals[f]);
            }

            // Add the liquid's exposed surface.
            bool horizontalSurfaceAtEnd =
                sin < 0.00001f &&
                (Mathf.Abs(height) < Epsilon ||
                 Mathf.Abs(height + cos * depth) < Epsilon);

            if (cutPoints.Count >= 3 && !horizontalSurfaceAtEnd)
            {
                Vector3 surfaceNormal = new Vector3(
                    -sin * lowSide.x,
                    cos,
                    -sin * lowSide.y
                ).normalized;

                Vector3 center = Vector3.zero;

                for (int i = 0; i < cutPoints.Count; i++)
                    center += cutPoints[i];

                center /= cutPoints.Count;

                Vector3 u = Vector3.Cross(surfaceNormal, Vector3.up);

                if (u.sqrMagnitude < Epsilon)
                    u = Vector3.Cross(surfaceNormal, Vector3.right);

                u.Normalize();

                Vector3 v = Vector3.Cross(surfaceNormal, u).normalized;

                pointComparer.Center = center;
                pointComparer.U = u;
                pointComparer.V = v;

                cutPoints.Sort(pointComparer);
                AddPolygon(cutPoints, surfaceNormal);
            }

            Mesh.Clear();
            Mesh.SetVertices(vertices);
            Mesh.SetNormals(normals);
            Mesh.SetTriangles(triangles, 0);

            Mesh.bounds = new Bounds(
                new Vector3(0f, -depth * 0.5f, 0f),
                new Vector3(halfWidth * 2f, depth, halfLength * 2f));
        }

        public static float FractionBelow(
            float height,
            float halfWidth,
            float halfLength,
            float depth,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            halfWidth = Mathf.Max(halfWidth, 0.0001f);
            halfLength = Mathf.Max(halfLength, 0.0001f);
            depth = Mathf.Max(depth, 0.0001f);
            cos = Mathf.Max(cos, 0.0001f);

            if (lowSide.sqrMagnitude > Epsilon)
                lowSide.Normalize();
            else
                lowSide = Vector2.right;

            // Integrate the filled height of vertical columns across the
            // rectangular floor. Midpoint sampling handles tilted surfaces.
            float sum = 0f;

            for (int ix = 0; ix < Samples; ix++)
            {
                float x = -halfWidth +
                    (ix + 0.5f) * (2f * halfWidth / Samples);

                for (int iz = 0; iz < Samples; iz++)
                {
                    float z = -halfLength +
                        (iz + 0.5f) * (2f * halfLength / Samples);

                    float surfaceY =
                        (height + sin *
                            (x * lowSide.x + z * lowSide.y)) / cos;

                    float columnHeight = Mathf.Clamp(
                        surfaceY + depth, 0f, depth);

                    sum += columnHeight;
                }
            }

            return Mathf.Clamp01(
                sum / (Samples * Samples * depth));
        }

        public static float RimHeight(
            float halfWidth,
            float halfLength,
            float sin,
            Vector2 lowSide)
        {
            if (lowSide.sqrMagnitude > Epsilon)
                lowSide.Normalize();

            float maxProjection =
                Mathf.Abs(lowSide.x) * halfWidth +
                Mathf.Abs(lowSide.y) * halfLength;

            return -sin * maxProjection;
        }

        public static float SolveHeight(
            float fraction,
            float halfWidth,
            float halfLength,
            float depth,
            float sin,
            float cos,
            Vector2 lowSide)
        {
            fraction = Mathf.Clamp01(fraction);
            depth = Mathf.Max(depth, 0.0001f);
            cos = Mathf.Max(cos, 0.0001f);

            if (lowSide.sqrMagnitude > Epsilon)
                lowSide.Normalize();
            else
                lowSide = Vector2.right;

            float projection =
                Mathf.Abs(lowSide.x) * halfWidth +
                Mathf.Abs(lowSide.y) * halfLength;

            float hi = RimHeight(
                halfWidth, halfLength, sin, lowSide);

            float atRim = FractionBelow(
                hi, halfWidth, halfLength,
                depth, sin, cos, lowSide);

            // Prevent the liquid surface from rising above the opening.
            if (fraction >= atRim)
                return hi;

            float lo = -cos * depth - sin * projection;

            for (int i = 0; i < 22; i++)
            {
                float mid = (lo + hi) * 0.5f;

                float current = FractionBelow(
                    mid, halfWidth, halfLength,
                    depth, sin, cos, lowSide);

                if (current < fraction)
                    lo = mid;
                else
                    hi = mid;
            }

            return (lo + hi) * 0.5f;
        }
    }
}
