using UnityEngine;

namespace MilkIt.Fluids
{
    public enum FluidState { Liquid, Granular }

    /// <summary>A pourable material: water, coffee, coffee grains, milk...</summary>
    [CreateAssetMenu(menuName = "MilkIt/Fluid Type", fileName = "NewFluidType")]
    public class FluidType : ScriptableObject
    {
        public string displayName;
        public Color color = Color.white;
        public FluidState state = FluidState.Liquid;
        [Tooltip("Optional. Material for streams, fill levels and puddles of this type. Tinted with 'color'.")]
        public Material material;

        public bool IsGranular => state == FluidState.Granular;
    }
}
