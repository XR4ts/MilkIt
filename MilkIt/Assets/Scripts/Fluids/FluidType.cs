using UnityEngine;

namespace MilkIt.Fluids
{
    public enum FluidState { Liquid, Granular }

    [CreateAssetMenu(menuName = "MilkIt/Fluid Type", fileName = "NewFluidType")]
    public class FluidType : ScriptableObject
    {
        public string displayName;
        public Color color = Color.white;
        [Min(0f)]
        [Tooltip("How strongly this fluid affects the color when blended with other liquids.")]
        public float colorStrength = 1f;
        public FluidState state = FluidState.Liquid;
        [Tooltip("Optional. Material for streams, fill levels and puddles of this type. Tinted with 'color'.")]
        public Material material;
        public QualityLossModel qualityLossModel;

        public bool IsGranular => state == FluidState.Granular;
    }
    public abstract class QualityLossModel : ScriptableObject
    {
        [Tooltip("How often updateQuality should be called, about 2 ticks per second")]
        public float tickDelay = 1f;
        public abstract float updateQuality(FluidContainer fluidContainer, float currentQuality);
    }
}
