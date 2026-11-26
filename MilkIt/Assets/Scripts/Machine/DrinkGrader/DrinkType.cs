using UnityEngine;
using MilkIt.Fluids;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MilkIt.Machine.DrinkGrader{
    // TODO: Customers now accept correct drink, and pay according to the quality. Need to work on detection, so far cappucino works great, everything else is either not detected
    // correctly or is given an really low score for no reason. Incorprate expected drink size to make sure you cant give a drop of coffee and have it count as an espresso.

    [CreateAssetMenu(menuName = "MilkIt/Drink Type")]
    public class DrinkType : ScriptableObject
    {
        public string name;
        public int price;
        public List<DrinkIngredient> ingredients;
        public float expectedDrinkMililiters;
        public Texture2D icon;
        public float MatchScore(IReadOnlyList<FluidLayer> fluidLayers, float totalFluids)
        {
            float totalError = 0;
            // float uncoutableError = 0f;
            foreach (DrinkIngredient ing in ingredients)
            {
                if (!fluidLayers.Any(lay => lay.type == ing.type)) totalError += 1;
            }
            foreach (FluidLayer layer in fluidLayers)
            {
                DrinkIngredient matchingIngredient = ingredients.FirstOrDefault(ing => ing.type == layer.type);
                if (matchingIngredient == null)
                {
                    totalError += 1;
                    continue;
                }
                float diff = Mathf.Abs((layer.amount / totalFluids) - matchingIngredient.ingredientPercentageInDrink);
                float volumeDiff = Mathf.Abs(totalFluids - expectedDrinkMililiters) / Mathf.Max(totalFluids, expectedDrinkMililiters);
                totalError += diff + volumeDiff;
            }
            if (totalError > 4) return 0;
            return (4 - totalError) / 4;
        }
    }
    [Serializable]
    public class DrinkIngredient
    {
        public FluidType type;
        [Range(0f, 1f)]
        public float ingredientPercentageInDrink;
    }
}