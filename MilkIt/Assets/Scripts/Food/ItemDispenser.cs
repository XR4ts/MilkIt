using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Food
{
    /// <summary>
    /// Keeps one item on a plate. When the item is carried further than <see cref="radius"/> from
    /// the spawn point, or destroyed, a new one is dropped onto the plate.
    /// </summary>
    public class ItemDispenser : MonoBehaviour
    {
        [Tooltip("The item to spawn, e.g. the Croissant prefab.")]
        public GameObject itemPrefab;
        [Tooltip("Centre of the plate's top surface, +Y up. Defaults to this transform.")]
        public Transform spawnPoint;
        [Tooltip("Height above the spawn point at which the item's origin appears. A bit more than half the item's height gives a small drop.")]
        public float dropHeight = 0.06f;
        [Tooltip("Random tilt in degrees so the item lands a little crooked and settles.")]
        public float randomTilt = 8f;
        [Tooltip("A new item appears once the current one is further than this from the spawn point.")]
        public float radius = 0.25f;

        public UnityEvent<GameObject> onSpawned = new UnityEvent<GameObject>();

        GameObject current;

        public GameObject Current => current;
        Transform Point => spawnPoint != null ? spawnPoint : transform;

        void Start()
        {
            if (itemPrefab == null)
            {
                Debug.LogWarning($"{name}: no item prefab set.", this);
                enabled = false;
                return;
            }
            Spawn();
        }

        void Update()
        {
            if (current != null && (current.transform.position - Point.position).sqrMagnitude < radius * radius) return;
            Spawn();
        }

        void Spawn()
        {
            Transform p = Point;
            Quaternion rotation = p.rotation * Quaternion.Euler(
                Random.Range(-randomTilt, randomTilt), Random.Range(0f, 360f), Random.Range(-randomTilt, randomTilt));
            current = Instantiate(itemPrefab, p.position + p.up * dropHeight, rotation);
            onSpawned.Invoke(current);
        }

        void OnDrawGizmosSelected()
        {
            Transform p = Point;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p.position, radius);
            Gizmos.DrawLine(p.position, p.position + p.up * dropHeight);
        }
    }
}
