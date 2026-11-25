using UnityEngine;

namespace MilkIt.Fluids
{   
    [CreateAssetMenu(menuName = "MilkIt/Qualitry Loss/Must Refrigerate")]
    public class MustRefrigerate : QualityLossModel
    {
        public override float updateQuality(FluidContainer fluidContainer, float currentQuality)
        {
            if (fluidContainer.isRefrigerated) return currentQuality;
            return currentQuality * 0.995f;
        }
    }
}
