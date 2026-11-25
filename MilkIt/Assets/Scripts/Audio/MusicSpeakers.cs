using UnityEngine;
using MilkIt.NPCs;

namespace MilkIt.Audio
{
    public class MusicSpeakers : MonoBehaviour
    {
        [SerializeField] AudioSource leftSpeaker;
        [SerializeField] AudioSource rightSpeaker;

        [Tooltip("Mono file with the song's LEFT channel.")]
        [SerializeField] AudioClip leftChannel;
        [Tooltip("Mono file with the song's RIGHT channel. Must be exactly the same length as the left.")]
        [SerializeField] AudioClip rightChannel;

        [Tooltip("Music volume (0–1). Background music should sit well under everything else.")]
        [SerializeField, Range(0f, 1f)] float volume = 0.35f;

        [Tooltip("Only play while the café is OPEN. The music keeps running silently, so it stays in sync.")]
        [SerializeField] bool onlyWhenOpen;

        [Tooltip("Seconds to fade in or out (at the start, and when the café opens or closes).")]
        [SerializeField, Min(0.01f)] float fadeSeconds = 2f;

        CafeService service;
        float level;

        void Start()
        {
            if (leftSpeaker == null || rightSpeaker == null || leftChannel == null || rightChannel == null)
            {
                Debug.LogWarning("MusicSpeakers: assign both speakers and both channel clips.", this);
                enabled = false;
                return;
            }
            if (leftChannel.samples != rightChannel.samples)
                Debug.LogWarning("MusicSpeakers: left and right clips have different lengths, so they will drift apart when looping.", this);

            SetUp(leftSpeaker, leftChannel);
            SetUp(rightSpeaker, rightChannel);

            double startAt = AudioSettings.dspTime + 0.2;
            leftSpeaker.PlayScheduled(startAt);
            rightSpeaker.PlayScheduled(startAt);

            if (onlyWhenOpen)
            {
                service = FindFirstObjectByType<CafeService>();
                if (service == null) Debug.LogWarning("MusicSpeakers: no CafeService found, so the music will stay silent.", this);
            }
        }

        static void SetUp(AudioSource speaker, AudioClip clip)
        {
            speaker.Stop();
            speaker.clip = clip;
            speaker.loop = true;
            speaker.playOnAwake = false;
            speaker.volume = 0f;
        }

        void Update()
        {
            bool shouldPlay = !onlyWhenOpen || (service != null && service.IsOpen);
            level = Mathf.MoveTowards(level, shouldPlay ? 1f : 0f, Time.deltaTime / fadeSeconds);

            float v = volume * level;
            leftSpeaker.volume = v;
            rightSpeaker.volume = v;
        }
    }
}