using System.Collections.Generic;
using UnityEngine;
using MilkIt.Fluids;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class PourSoundManager : MonoBehaviour
    {
        [Tooltip("Seamless loop of liquid pouring.")]
        [SerializeField] AudioClip liquidLoop;

        [Tooltip("Seamless loop of grains trickling (coffee beans, ground coffee). Leave empty to keep them silent.")]
        [SerializeField] AudioClip granularLoop;

        [Tooltip("Seconds to fade in when a pour starts and out when it stops. Prevents clicks.")]
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.1f;

        [Tooltip("Random pitch change per pour, so pours don't all sound the same.")]
        [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.05f;

        [Tooltip("How often (seconds) to look for new streams. A stream is created the first time something pours.")]
        [SerializeField, Min(0.05f)] float scanInterval = 0.25f;

        class Voice
        {
            public FluidStream stream;
            public AudioSource source;
        }

        readonly List<Voice> voices = new List<Voice>();
        AudioSource template;
        float nextScan;

        void Awake()
        {
            template = GetComponent<AudioSource>();
            template.playOnAwake = false;
            template.Stop();
        }

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + scanInterval;
                FindNewStreams();
            }

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice v = voices[i];
                if (v.stream == null || v.source == null)
                {
                    if (v.source != null) Destroy(v.source.gameObject);
                    voices.RemoveAt(i);
                    continue;
                }
                UpdateVoice(v);
            }
        }

        void FindNewStreams()
        {
            foreach (var stream in FindObjectsByType<FluidStream>(FindObjectsSortMode.None))
            {
                bool known = false;
                foreach (var v in voices)
                    if (v.stream == stream) { known = true; break; }

                if (!known) voices.Add(new Voice { stream = stream, source = CreateSource(stream) });
            }
        }

        AudioSource CreateSource(FluidStream stream)
        {
            var go = new GameObject("Pour Sound");
            go.transform.SetParent(stream.transform, false);

            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.volume = 0f;
            s.outputAudioMixerGroup = template.outputAudioMixerGroup;
            s.priority = template.priority;
            s.spatialBlend = template.spatialBlend;
            s.dopplerLevel = template.dopplerLevel;
            s.rolloffMode = template.rolloffMode;
            s.minDistance = template.minDistance;
            s.maxDistance = template.maxDistance;
            s.spread = template.spread;
            return s;
        }

        void UpdateVoice(Voice v)
        {
            AudioSource s = v.source;
            AudioClip clip = IsGranular(v.stream) ? granularLoop : liquidLoop;
            bool pouring = v.stream.isActiveAndEnabled && v.stream.IsPouring && clip != null;

            s.transform.position = SoundPosition(v.stream);

            float target = pouring ? template.volume : 0f;
            s.volume = Mathf.MoveTowards(s.volume, target, template.volume * Time.deltaTime / fadeSeconds);

            if (pouring && !s.isPlaying)
            {
                s.clip = clip;
                s.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                s.Play();
                s.time = Random.Range(0f, clip.length * 0.9f);
            }
            else if (pouring && s.clip != clip)
            {
                s.clip = clip;
                s.Play();
            }
            else if (!pouring && s.isPlaying && s.volume <= 0f)
            {
                s.Stop();
            }
        }

        static Vector3 SoundPosition(FluidStream stream)
        {
            if (stream.owner != null) return stream.owner.Opening.position;
            if (stream.TryGetComponent(out FluidSource source) && source.spout != null) return source.spout.position;
            return stream.transform.position;
        }

        static bool IsGranular(FluidStream stream)
        {
            if (stream.owner == null)
                return stream.TryGetComponent(out FluidSource source) && source.fluidType != null && source.fluidType.IsGranular;

            FluidType main = null;
            float most = 0f;
            foreach (var layer in stream.owner.Layers)
                if (layer.amount > most) { most = layer.amount; main = layer.type; }
            return main != null && main.IsGranular;
        }
    }
}