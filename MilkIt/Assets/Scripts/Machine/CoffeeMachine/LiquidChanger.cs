using UnityEngine;
using MilkIt;
using MilkIt.Fluids;

namespace MilkIt.Machine.CoffeeMachine{
    public class LiquidChanger : MonoBehaviour
    {

        [SerializeField] private FluidType fromFluid; 
        [SerializeField] private FluidType toFluid; 
        [SerializeField] private float transformScale = 1f;
        [SerializeField] private float takenScale = 1f;
        protected bool shouldBreakFlag = false;
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer))
                TransformFluid(fluidContainer);
            else
            {
                if (other.transform.parent.parent == null) return;
                if (other.transform.parent.parent.TryGetComponent<FluidContainer>(out FluidContainer grandpaFluidContainer))
                {
                    TransformFluid(grandpaFluidContainer);
                }
                else
                {
                    Debug.Log($"{this} expected either collider or colliders grandparent to be a fluid container");
                }
            }
                
        }

        private void OnTriggerExit(Collider other)
        {
            shouldBreakFlag = false;
        }

        private void TransformFluid(FluidContainer fluidContainer)
        {

            if (shouldBreakFlag) return;

            float taken = fluidContainer.Remove(fromFluid, transformScale);
            if (taken == 0f)
            {
                shouldBreakFlag = true;
                return;
            }
            fluidContainer.Add(toFluid, taken * takenScale);
        }
    }
}