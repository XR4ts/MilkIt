using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    // Small, self-contained test-room bake using Unity's built-in navigation API.
    // A production cafe can supply its own baked NavMesh instead of using this component.
    [DefaultExecutionOrder(-100)]
    public sealed class PrototypeNavigation : MonoBehaviour
    {
        public BoxCollider floor;
        public BoxCollider[] obstacles;
        NavMeshData data;
        NavMeshDataInstance instance;

        void OnEnable()
        {
            if (floor == null) return;
            var sources = new List<NavMeshBuildSource> { Source(floor, 0) };
            foreach (var obstacle in obstacles)
                if (obstacle != null) sources.Add(Source(obstacle, 1));
            var bounds = floor.bounds;
            bounds.Expand(new Vector3(2, 8, 2));
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                bounds, Vector3.zero, Quaternion.identity);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
            else Debug.LogError("Could not bake the prototype navigation mesh.", this);
        }

        static NavMeshBuildSource Source(BoxCollider box, int area) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center),
            size = box.size,
            area = area
        };

        void OnDisable()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }
    }
}
