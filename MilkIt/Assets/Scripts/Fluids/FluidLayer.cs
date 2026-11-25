using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.Fluids
{
    [Serializable]
    public class FluidLayer
    {
        public FluidType type;
        public float amount;
        
        public float quality;
        public bool isHot;
        private float lastTick;
        public FluidLayer(FluidType type, float amount)
        {
            this.type = type;
            this.amount = amount;
            this.quality = 1f;
            this.lastTick = type.qualityLossModel != null ? type.qualityLossModel.tickDelay : 0f;
            this.isHot = type.shouldBeHot;
        }
        public void TickQuality(FluidContainer fluidContainer, float deltaTime)
        {
            if (!type || !type.qualityLossModel) return;
            if (lastTick > 0f)
            {
                lastTick -= deltaTime;
                return;
            }
            lastTick = type.qualityLossModel.tickDelay;
            quality = type.qualityLossModel.updateQuality(fluidContainer, quality);
            if (quality < 0.7) isHot = false;
        }
    }

    public static class FluidMix
    {
        public static float Total(List<FluidLayer> layers)
        {
            float total = 0f;
            for (int i = 0; i < layers.Count; i++) total += layers[i].amount;
            return total;
        }
        public static void Add(List<FluidLayer> layers, FluidType type, float amount)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].type != type) continue;
                layers[i] = new FluidLayer(type, layers[i].amount + amount);
                return;
            }
            layers.Add(new FluidLayer(type, amount));
        }

        public static float BlendQualities(IReadOnlyList<FluidLayer> layers)
        {
            float total = 0f;
            int i = 0;
            while (i < layers.Count)
            {
                total += layers[i].quality;
                i++;
            }
            return total / i;
        }

        public static Color Blend(List<FluidLayer> layers, out FluidType dominant)
        {
            dominant = null;
        
            float bestAmount = 0f;
            float totalColorWeight = 0f;
        
            float red = 0f;
            float green = 0f;
            float blue = 0f;
        
            // Accumulate opacity independently of RGB color.
            float transparencyProduct = 1f;
        
            for (int i = 0; i < layers.Count; i++)
            {
                FluidLayer layer = layers[i];
        
                if (layer.type == null || layer.amount <= 0f)
                    continue;
        
                FluidType type = layer.type;
                Color c = type.color;
                float amount = layer.amount;
        
                // RGB contribution depends on tint strength, not opacity.
                float weight = amount * Mathf.Max(0f, type.colorStrength);
        
                red += c.r * weight;
                green += c.g * weight;
                blue += c.b * weight;
                totalColorWeight += weight;
        
                // Alpha is handled separately.
                // Assumes amount is a normalized coverage/depth measure.
                float alpha = Mathf.Clamp01(c.a);
                transparencyProduct *= Mathf.Pow(1f - alpha, amount);
        
                if (amount > bestAmount)
                {
                    bestAmount = amount;
                    dominant = type;
                }
            }
        
            if (dominant == null)
                return Color.white;
        
            Color result;
        
            if (totalColorWeight > 0f)
            {
                result = new Color(
                    red / totalColorWeight,
                    green / totalColorWeight,
                    blue / totalColorWeight,
                    1f
                );
            }
            else
            {
                // No fluid contributes RGB color.
                result = Color.white;
            }
        
            result.a = 1f - transparencyProduct;
        
            return result;
        }
    }
}
