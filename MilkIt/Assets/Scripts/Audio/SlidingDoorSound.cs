using UnityEngine;
using MilkIt.NPCs;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class SlidingDoorSound : MonoBehaviour
    {
        [Tooltip("Leave empty to find the café's sliding door automatically.")]
        [SerializeField] CafeSlidingDoor door;

        [Tooltip("Pitch when opening. 1 = original sound.")]
        [SerializeField, Range(0.5f, 1.5f)] float openPitch = 1f;

        [Tooltip("Pitch when closing. Slightly lower makes closing sound a bit different.")]
        [SerializeField, Range(0.5f, 1.5f)] float closePitch = 0.95f;

        AudioSource source;
        float lastAmount;
        int direction;

        void Start()
        {
            source = GetComponent<AudioSource>();

            if (door == null) door = FindFirstObjectByType<CafeSlidingDoor>();
            if (door == null)
            {
                Debug.LogWarning("SlidingDoorSound: no CafeSlidingDoor found in the scene.", this);
                enabled = false;
                return;
            }
            lastAmount = door.OpenAmount;
        }

        void Update()
        {
            float amount = door.OpenAmount;

            if (amount > lastAmount && direction != 1)
            {
                direction = 1;
                PlaySlide(openPitch);
            }
            else if (amount < lastAmount && direction != -1)
            {
                direction = -1;
                PlaySlide(closePitch);
            }

            lastAmount = amount;
        }

        void PlaySlide(float pitch)
        {
            source.pitch = pitch;
            source.Play();
        }
    }
}