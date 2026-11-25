using UnityEngine;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class LeverSound : MonoBehaviour
    {
        [Tooltip("Leave empty to use the Hinge Joint on this object.")]
        [SerializeField] HingeJoint hinge;

        [Tooltip("How far the lever must be pulled (0 = rest, 1 = all the way) before the pull sound plays.")]
        [SerializeField, Range(0f, 1f)] float pullAt = 0.25f;

        [Tooltip("How far back it must return before the release sound plays. Keep this lower than Pull At.")]
        [SerializeField, Range(0f, 1f)] float releaseAt = 0.1f;

        [SerializeField] bool playOnRelease = true;
        [SerializeField, Range(0.5f, 1.5f)] float pullPitch = 1f;
        [SerializeField, Range(0.5f, 1.5f)] float releasePitch = 0.9f;

        [Tooltip("Random pitch change each time, so repeated pulls don't sound identical.")]
        [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.04f;

        AudioSource source;
        float travel;
        bool pulled;

        void Start()
        {
            source = GetComponent<AudioSource>();
            if (hinge == null) hinge = GetComponent<HingeJoint>();
            if (hinge == null)
            {
                Debug.LogWarning("LeverSound: no Hinge Joint found.", this);
                enabled = false;
                return;
            }
            travel = Mathf.Max(1f, hinge.limits.max - hinge.limits.min); // 150° on the espresso machine
        }

        void Update()
        {
            float amount = Mathf.Abs(hinge.angle) / travel; // 0 = at rest, 1 = fully pulled

            if (!pulled && amount >= pullAt)
            {
                pulled = true;
                Play(pullPitch);
            }
            else if (pulled && amount <= releaseAt)
            {
                pulled = false;
                if (playOnRelease) Play(releasePitch);
            }
        }

        void Play(float pitch)
        {
            source.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }
    }
}