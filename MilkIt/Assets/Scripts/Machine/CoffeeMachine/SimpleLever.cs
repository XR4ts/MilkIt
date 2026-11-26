using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using MilkIt.Fluids;

namespace MilkIt.Machine.CoffeeMachine{
    public class SimpleLever : MonoBehaviour
    {
        [SerializeField] private HingeJoint hinge;
        [SerializeField] internal FluidSource fluidSource;
        [SerializeField] private FluidSource alternativeFluidSource;

        [Header("Return")]
        [Tooltip("Spring strength pulling the lever back up once it's let go. 0 = it stays where it was left.")]
        [SerializeField, Min(0f)] private float returnSpring = 100f;

        private float total;
        private XRBaseInteractable grab;

        void Awake()
        {
            if (hinge == null) hinge = GetComponent<HingeJoint>();
            grab = hinge != null ? hinge.GetComponent<XRBaseInteractable>() : GetComponent<XRBaseInteractable>();
            if (grab != null)
            {
                grab.selectEntered.AddListener(_ => SetReturnSpring(false));
                grab.selectExited.AddListener(_ => { if (!grab.isSelected) SetReturnSpring(true); });
            }
        }

        void Start()
        {
            total = hinge.limits.max - hinge.limits.min;
            SetReturnSpring(grab == null || !grab.isSelected);
        }

        // Off while held so the spring doesn't fight the hand.
        void SetReturnSpring(bool on)
        {
            if (hinge == null) return;
            var spring = hinge.spring;
            spring.spring = on ? returnSpring : 0f;
            hinge.spring = spring;
            hinge.useSpring = true;
            var body = hinge.GetComponent<Rigidbody>();
            if (body != null) body.WakeUp();
        }

        void Update()
        {
            float angle = Mathf.Abs(Mathf.Round(hinge.angle * 0.8f)) / total;
            if (angle <= 0.01f) angle = 0f;
            else if (angle > 0.999f) angle = 0.999f;

            if (float.IsNaN(angle)) angle = 0f;
            if (fluidSource != null)
            {
                fluidSource.SetFlowScale(angle);
                if (angle > 0f)
                {
                    fluidSource.Dispense();
                }
                else
                {
                    fluidSource.StopFlow();
                }
                if (alternativeFluidSource != null) alternativeFluidSource.SetFlowScale(0);
                return;
            }
            if (alternativeFluidSource != null)
            {
                alternativeFluidSource.SetFlowScale(angle);
                return;
            }
        }
    }
}
