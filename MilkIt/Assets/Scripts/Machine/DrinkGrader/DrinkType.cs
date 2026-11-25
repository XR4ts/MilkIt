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
        public bool RoughlyMatches(List<FluidLayer> fluidLayers, float totalFluids, float acceptableError = 0.5f)
        {
            return false;
        }

        public float MatchError(IReadOnlyList<FluidLayer> fluidLayers, float totalFluids)
        {
            float totalError = 0;
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
                Debug.Log($"{layer}");
                Debug.Log($"Taking up {layer.amount} / {totalFluids} percentage of : {layer.amount / totalFluids}");
                Debug.Log($"Expected {matchingIngredient.ingredientPercentageInDrink}");
                float diff = Mathf.Abs((layer.amount / totalFluids) - matchingIngredient.ingredientPercentageInDrink);
                Debug.Log($"Gives error {diff}");
                totalError += diff;
            }
            return totalError;
        }
        public float MatchScore(IReadOnlyList<FluidLayer> fluidLayers, float totalFluids)
        {
            float totalError = 0;
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
                Debug.Log($"Counting {layer}: {diff}");
                totalError += diff;
            }
            if (totalError > 3) return 0;
            return (3 - totalError) / 3;
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