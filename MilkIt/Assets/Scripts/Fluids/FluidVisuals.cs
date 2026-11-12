using UnityEngine;
using UnityEngine.Rendering;

namespace MilkIt.Fluids
{
    /// <summary>Shared mesh/material helpers so every stream, fill level and puddle is one cheap cylinder.</summary>
    internal static class FluidVisuals
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static Mesh cylinder;
        static Material liquidFallback, granularFallback;
        static MaterialPropertyBlock block;

        /// <summary>Cylinder with radius 0.5 and height 2, so localScale = (diameter, height / 2, diameter).</summary>
        public static Mesh Cylinder
        {
            get
            {
                if (cylinder == null) cylinder = BuildCylinder(24);
                return cylinder;
            }
        }

        // Built in code rather than taken from Unity's built-in resources, so its size is known for certain.
        static Mesh BuildCylinder(int sides)
        {
            var vertices = new Vector3[sides * 4 + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[sides * 12];
            int sideBottom = 0, sideTop = sides, capBottom = sides * 2, capTop = sides * 3;
            int centerBottom = sides * 4, centerTop = sides * 4 + 1;

            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 bottom = outward * 0.5f + Vector3.down, top = outward * 0.5f + Vector3.up;
                vertices[sideBottom + i] = bottom; normals[sideBottom + i] = outward;
                vertices[sideTop + i] = top; normals[sideTop + i] = outward;
                vertices[capBottom + i] = bottom; normals[capBottom + i] = Vector3.down;
                vertices[capTop + i] = top; normals[capTop + i] = Vector3.up;

                int j = (i + 1) % sides, t = i * 12;
                triangles[t] = sideBottom + i; triangles[t + 1] = sideTop + i; triangles[t + 2] = sideTop + j;
                triangles[t + 3] = sideBottom + i; triangles[t + 4] = sideTop + j; triangles[t + 5] = sideBottom + j;
                triangles[t + 6] = centerTop; triangles[t + 7] = capTop + j; triangles[t + 8] = capTop + i;
                triangles[t + 9] = centerBottom; triangles[t + 10] = capBottom + i; triangles[t + 11] = capBottom + j;
            }
            vertices[centerBottom] = Vector3.down; normals[centerBottom] = Vector3.down;
            vertices[centerTop] = Vector3.up; normals[centerTop] = Vector3.up;

            var mesh = new Mesh { name = "Fluid Cylinder", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material MaterialFor(FluidType type)
        {
            if (type != null && type.material != null) return type.material;
            bool granular = type != null && type.IsGranular;
            if (granular)
            {
                if (granularFallback == null) granularFallback = MakeFallback(0.05f);
                return granularFallback;
            }
            if (liquidFallback == null) liquidFallback = MakeFallback(0.9f);
            return liquidFallback;
        }

        static Material MakeFallback(float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "Fluid (fallback)" };
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static MeshRenderer CreateCylinder(string name, Transform parent) => CreateRenderer(name, parent, Cylinder);

        public static MeshRenderer CreateRenderer(string name, Transform parent, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sharedMaterial = MaterialFor(null);
            return r;
        }

        public static void Apply(Renderer r, Color color, FluidType dominant)
        {
            Material m = MaterialFor(dominant);
            if (r.sharedMaterial != m) r.sharedMaterial = m;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
        }
    }
}
