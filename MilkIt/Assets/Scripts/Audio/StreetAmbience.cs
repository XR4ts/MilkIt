using UnityEngine;
using MilkIt.NPCs;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public class StreetAmbience : MonoBehaviour
    {
        [Tooltip("Leave empty to find the café automatically.")]
        [SerializeField] CafeCustomerSystem cafe;

        [Tooltip("Volume compared with normal (0–1) while you're inside and the door is closed.")]
        [SerializeField, Range(0f, 1f)] float closedVolume = 0.4f;

        [Tooltip("How muffled it is inside with the door closed (Hz). Lower = more muffled.")]
        [SerializeField, Range(200f, 5000f)] float closedCutoff = 900f;

        const float OpenCutoff = 22000f;

        AudioSource source;
        AudioLowPassFilter filter;
        Transform listener;
        CafeSlidingDoor door;
        Vector3 inward;
        float baseVolume;

        void Start()
        {
            source = GetComponent<AudioSource>();
            filter = GetComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = OpenCutoff;
            baseVolume = source.volume;

            if (cafe == null) cafe = FindFirstObjectByType<CafeCustomerSystem>();
            var audioListener = FindFirstObjectByType<AudioListener>(); // on the VR camera
            if (cafe == null || cafe.entranceDoor == null || cafe.entrance == null || audioListener == null)
            {
                Debug.LogWarning("StreetAmbience: café door or audio listener not found, so the street won't be muffled.", this);
                enabled = false;
                return;
            }

            door = cafe.entranceDoor;
            listener = audioListener.transform;
            inward = Vector3.ProjectOnPlane(door.transform.position - cafe.entrance.position, Vector3.up).normalized;
        }

        void Update()
        {
            bool listenerInside = Vector3.Dot(listener.position - door.transform.position, inward) > 0f;

            float muffle = listenerInside ? 1f - door.OpenAmount : 0f;

            source.volume = baseVolume * Mathf.Lerp(1f, closedVolume, muffle);
            filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(closedCutoff), muffle));
        }
    }
}