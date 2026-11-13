using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    // Drives a Humanoid character visual (see CharacterPreviewSetup) for a customer.
    // While Locomotion is on, it picks Walk/Idle/Talk from movement; one-shot actions turn it off.
    public sealed class CustomerAnimator : MonoBehaviour
    {
        public static readonly int Idle = Animator.StringToHash("Idle");
        public static readonly int Walk = Animator.StringToHash("Walk");
        public static readonly int Talk = Animator.StringToHash("Talk");
        public static readonly int SitDown = Animator.StringToHash("SitDown");
        public static readonly int SitIdle = Animator.StringToHash("SitIdle");
        public static readonly int SitTalk = Animator.StringToHash("SitTalk");
        public static readonly int StandUp = Animator.StringToHash("StandUp");
        static readonly int Drink = Animator.StringToHash("Drink");
        const int ArmLayer = 1;

        [Tooltip("Ground speed (m/s) the Walk clip is authored for; playback is scaled to match.")]
        public float walkClipSpeed = 0.95f; // measured from the library's root-motion Walk_Loop

        public bool Locomotion { get; set; } = true;
        public bool Talking { get; set; }
        public float KinematicSpeed { get; set; }
        public Animator Animator => animator;

        Animator animator;
        NavMeshAgent agent;
        GameObject cup;
        int current;

        public void Init(Animator characterAnimator, NavMeshAgent navigation)
        {
            animator = characterAnimator;
            agent = navigation;
            var hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var found = hand != null ? hand.Find("Cup") : null;
            cup = found != null ? found.gameObject : null;
            ShowCup(false);
        }

        void Update()
        {
            if (animator == null || !Locomotion) return;
            var speed = agent != null && agent.enabled ? agent.velocity.magnitude : KinematicSpeed;
            var walking = speed > 0.15f;
            var target = walking ? Walk : Talking ? Talk : Idle;
            if (target != current) Play(target, 0.2f, keepLocomotion: true);
            animator.speed = walking ? Mathf.Clamp(speed / walkClipSpeed, 0.6f, 1.6f) : 1f;
        }

        public void Play(int state, float fade = 0.2f, bool keepLocomotion = false)
        {
            if (animator == null) return;
            if (!keepLocomotion) { Locomotion = false; animator.speed = 1f; }
            current = state;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        // Plays a one-shot body clip and waits until it has (nearly) finished.
        public IEnumerator PlayAndWait(int state, float timeout = 6f)
        {
            Play(state);
            var started = Time.time;
            yield return null;
            // Wait for the cross-fade to reach the state, then for it to end or hand over to its follow-up.
            while (Time.time - started < 1f && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != state)
                yield return null;
            while (Time.time - started < timeout)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != state || info.normalizedTime >= 0.95f) break;
                yield return null;
            }
        }

        public void Sip()
        {
            if (animator != null && animator.layerCount > ArmLayer)
                animator.CrossFadeInFixedTime(Drink, 0.3f, ArmLayer, 0f);
        }

        public void ShowCup(bool visible)
        {
            if (cup != null) cup.SetActive(visible);
        }
    }
}
