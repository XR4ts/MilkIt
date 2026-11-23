using UnityEngine;
using MilkIt.Fluids;
using MilkIt.Machine;
using System.Collections.Generic;

namespace MilkIt.Machine.DrinkGrader{
    public static class DrinkGrader
    {
        private static DrinkType[] drinkTypes;
        private static void EnsureLoaded()
        {
            if (drinkTypes == null)
            {
                drinkTypes = Resources.LoadAll<DrinkType>("DrinkTypes");

                if (drinkTypes.Length == 0)
                {
                    Debug.LogWarning(
                        "No DrinkTypes found in Resources/DrinkTypes!"
                    );
                }
            }
        }

        public static (float drinkScore, DrinkType predictedDrink) GradeDrink(FluidContainer fluidContainer)
        {
            EnsureLoaded();

            DrinkType bestGuess = null;
            float bestScore = -99999f;
            foreach (DrinkType drink in drinkTypes)
            {
                float score = drink.MatchScore(fluidContainer.Layers, fluidContainer.Total);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestGuess = drink;
                }
            }
            if (bestGuess == null)
            {
                Debug.Log("Something went wrong, probably there are no DrinkTypes defined!");
            }
            float drinkScore = FluidMix.BlendQualities(fluidContainer.Layers) * bestScore;
            Debug.Log($"Drink is probably {bestGuess} with guessing score: {bestScore}. Drink score: {drinkScore}");
            return (drinkScore, bestGuess);
        }
    }
}