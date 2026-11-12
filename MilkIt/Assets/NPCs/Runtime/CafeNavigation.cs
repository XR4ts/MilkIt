using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    [DefaultExecutionOrder(-100)]
    public sealed class CafeNavigation : MonoBehaviour
    {
        public NavMeshData bakedData;
        NavMeshDataInstance instance;
        void OnEnable() { if (bakedData != null) instance = NavMesh.AddNavMeshData(bakedData); }
        void OnDisable() { if (instance.valid) instance.Remove(); }
    }
}
