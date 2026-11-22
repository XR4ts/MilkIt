using UnityEngine;
using MilkIt.Fluids;
using MilkIt.Machine;
using System.Collections.Generic;

namespace MilkIt.Machine.DrinkGrader{
    public class DrinkGrader : MonoBehaviour
    {
        private DrinkType[] drinkTypes;
        private void Start()
        {
            drinkTypes = Resources.LoadAll<DrinkType>("DrinkTypes");
        }
        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer))
                return;
            gradeDrinkError(fluidContainer);

        }

        void gradeDrink(FluidContainer fluidContainer)
        {
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
            // Debug.Log($"Drink is probably {bestGuess} with guessing score: {bestScore}. Drink score: {drinkScore}");
        }

        void gradeDrinkError(FluidContainer fluidContainer)
        {
            DrinkType bestGuess = null;
            float smallestError = 99999f;
            foreach (DrinkType drink in drinkTypes)
            {
                float err = drink.MatchError(fluidContainer.Layers, fluidContainer.Total);
                Debug.Log($"{drink} : {err}");
                if (err < smallestError)
                {
                    smallestError = err;
                    bestGuess = drink;
                }
            }
            if (bestGuess == null)
            {
                Debug.Log("Something went wrong, probably there are no DrinkTypes defined!");
            }
            // Debug.Log($"Drink is probably {bestGuess} with error: {smallestError}");
        }
    }
}