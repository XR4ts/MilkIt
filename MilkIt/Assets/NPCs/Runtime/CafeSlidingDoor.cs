using UnityEngine;

namespace MilkIt.NPCs
{
    public sealed class CafeSlidingDoor : MonoBehaviour
    {
        public CafeCustomerSystem customers;
        public Transform leftPanel, rightPanel;
        public Vector3 leftOpenOffset, rightOpenOffset;
        public Transform playerHead;
        [Min(0.1f)] public float openingSpeed = 2f;
        [Min(0)] public float closeDelay = 1f;
        public Vector3 sensorHalfSize = new Vector3(2.2f, 2f, 3f);
        public float OpenAmount { get; private set; }
        public bool IsOpen => OpenAmount >= 0.95f;
        Vector3 leftClosed, rightClosed;
        float lastPresence;

        void Awake()
        {
            leftClosed = leftPanel.localPosition;
            rightClosed = rightPanel.localPosition;
            lastPresence = -closeDelay;
        }

        void Update()
        {
            bool present = playerHead != null && playerHead.gameObject.activeInHierarchy && InSensor(playerHead.position);
            if (customers != null)
                foreach (var customer in customers.Customers)
                    present |= customer != null && InSensor(customer.transform.position);
            if (present) lastPresence = Time.time;
            bool open = present || Time.time - lastPresence < closeDelay;
            OpenAmount = Mathf.MoveTowards(OpenAmount, open ? 1f : 0f, openingSpeed * Time.deltaTime);
            leftPanel.localPosition = leftClosed + leftOpenOffset * OpenAmount;
            rightPanel.localPosition = rightClosed + rightOpenOffset * OpenAmount;
        }

        bool InSensor(Vector3 point)
        {
            var p = transform.InverseTransformPoint(point);
            return Mathf.Abs(p.x) < sensorHalfSize.x && Mathf.Abs(p.y) < sensorHalfSize.y &&
                   Mathf.Abs(p.z) < sensorHalfSize.z;
        }

        // The NavMesh routes through the doorway; customers wait on either side until it is open.
        public bool ShouldWait(Vector3 point)
        {
            var p = transform.InverseTransformPoint(point);
            return !IsOpen && Mathf.Abs(p.x) < sensorHalfSize.x && Mathf.Abs(p.z) < 1.2f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, sensorHalfSize * 2);
        }
    }
}
