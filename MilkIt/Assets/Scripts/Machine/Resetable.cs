using UnityEngine;

namespace MilkIt.Machine {
    public class Resetable : MonoBehaviour
    {
        private Vector3 startPosition;
        private Quaternion startRotation;

        void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        public void ResetObject()
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
    }
}