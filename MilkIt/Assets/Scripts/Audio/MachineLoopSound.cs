using UnityEngine;
using MilkIt.Fluids;

namespace MilkIt.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class MachineLoopSound : MonoBehaviour
    {
        [Tooltip("The machine to listen to. Leave empty to use the FluidSource on this object or a parent.")]
        [SerializeField] FluidSource machine;

        [Tooltip("Seconds to reach full speed after starting.")]
        [SerializeField, Min(0.01f)] float spinUpSeconds = 0.25f;

        [Tooltip("Seconds to wind down after stopping.")]
        [SerializeField, Min(0.01f)] float spinDownSeconds = 0.4f;

        [Tooltip("Pitch at the start of spin-up and end of spin-down (1 = normal). Lower sounds more like a motor.")]
        [SerializeField, Range(0.3f, 1f)] float idlePitch = 0.6f;

        AudioSource source;
        float baseVolume;
        float speed;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            baseVolume = source.volume;
            if (machine == null) machine = GetComponentInParent<FluidSource>();
            if (machine == null) Debug.LogWarning("MachineLoopSound: no FluidSource found.", this);
        }

        void Update()
        {
            if (machine == null || source.clip == null) return;

            bool running = machine.isActiveAndEnabled && machine.IsFlowing;
            speed = running
                ? Mathf.MoveTowards(speed, 1f, Time.deltaTime / spinUpSeconds)
                : Mathf.MoveTowards(speed, 0f, Time.deltaTime / spinDownSeconds);

            if (speed > 0f && !source.isPlaying)
            {
                source.Play();
                source.time = Random.Range(0f, source.clip.length * 0.9f);
            }
            else if (speed <= 0f && source.isPlaying)
            {
                source.Stop();
            }

            source.volume = baseVolume * speed;
            source.pitch = Mathf.Lerp(idlePitch, 1f, speed);
        }
    }
}