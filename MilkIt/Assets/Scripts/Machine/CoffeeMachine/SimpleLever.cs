using UnityEngine;
using MilkIt.Fluids;

namespace MilkIt.Machine.CoffeeMachine{
    public class SimpleLever : MonoBehaviour
    {
        [SerializeField] private HingeJoint hinge;
        [SerializeField] internal FluidSource fluidSource;
        [SerializeField] private FluidSource alternativeFluidSource;

        private float total;
        // TODO: GroundCoffee now works, but it is consumed, should it be transformed to used coffee grounds?
        void Start()
        {
            total = hinge.limits.max - hinge.limits.min;
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
