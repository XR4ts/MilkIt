using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.Fluids
{
    [Serializable]
    public struct FluidLayer
    {
        public FluidType type;
        public float amount;

        public FluidLayer(FluidType type, float amount)
        {
            this.type = type;
            this.amount = amount;
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

        public static Color Blend(List<FluidLayer> layers, out FluidType dominant)
        {
            dominant = null;
            float total = 0f, best = 0f;
            Color sum = Color.clear;
            for (int i = 0; i < layers.Count; i++)
            {
                FluidLayer l = layers[i];
                if (l.type == null || l.amount <= 0f) continue;
                sum += l.type.color * l.amount;
                total += l.amount;
                if (l.amount > best) { best = l.amount; dominant = l.type; }
            }
            if (total <= 0f) return Color.white;
            Color c = sum / total;
            c.a = 1f;
            return c;
        }
    }
}
