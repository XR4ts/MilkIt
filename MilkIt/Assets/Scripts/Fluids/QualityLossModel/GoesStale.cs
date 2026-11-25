using UnityEngine;

namespace MilkIt.Fluids
{   
    [CreateAssetMenu(menuName = "MilkIt/Qualitry Loss/Goes Stale")]
    public class GoesStale : QualityLossModel
    {
        public override float updateQuality(FluidContainer fluidContainer, float currentQuality)
        {
            return currentQuality * 0.99f;
        }
    }
}