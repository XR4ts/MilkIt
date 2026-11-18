using UnityEngine;
using MilkIt.Fluids;

namespace MilkIt.Machine.CoffeeMachine{
    public class SimpleLever : MonoBehaviour
    {
        [SerializeField] private HingeJoint hinge;
        [SerializeField] internal FluidSource fluidSource;
        [SerializeField] private FluidSource alternativeFluidSource;

        private float total;
        // TODO: I guess a source with a supply cannot be always on? Have to figure out something else. Guessing the dose is an issue. without a supplier this always on → manipulate flow speed, worked fine. With supplier it works no longer.
        // also, the `flask` detector for the grinder covering does not work. It doesnt have to tbh
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
