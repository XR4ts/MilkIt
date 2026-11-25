using UnityEngine;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class ImpactSound : MonoBehaviour
    {
        [Tooltip("One clip is enough. Add more variations and one is picked at random for each hit.")]
        [SerializeField] AudioClip[] clips;

        [Tooltip("Hits slower than this (metres per second) make no sound, so resting or sliding objects stay quiet.")]
        [SerializeField, Min(0f)] float minSpeed = 0.3f;

        [Tooltip("Hits at or above this speed play at full volume.")]
        [SerializeField, Min(0.1f)] float fullVolumeSpeed = 2.5f;

        [Tooltip("Volume of the softest hit that still makes a sound.")]
        [SerializeField, Range(0f, 1f)] float softestVolume = 0.15f;

        [Tooltip("Minimum seconds between sounds. One bump often touches several colliders at once.")]
        [SerializeField, Min(0f)] float cooldown = 0.1f;

        [Tooltip("No sounds for this long after the scene starts, while objects settle onto the table.")]
        [SerializeField, Min(0f)] float quietAtStart = 0.5f;

        [Tooltip("Random pitch change each hit, so repeated hits don't sound identical.")]
        [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.05f;

        AudioSource source;
        float lastPlayed = float.NegativeInfinity;

        void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.timeSinceLevelLoad < quietAtStart) return;
            if (Time.time - lastPlayed < cooldown) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;

            float strength = Mathf.InverseLerp(minSpeed, fullVolumeSpeed, speed); // 0 = soft, 1 = hard
            float volume = Mathf.Lerp(softestVolume, 1f, strength);

            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
            lastPlayed = Time.time;
        }
    }
}