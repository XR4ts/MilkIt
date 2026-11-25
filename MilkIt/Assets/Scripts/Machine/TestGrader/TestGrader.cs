using UnityEngine;
using MilkIt.Machine.DrinkGrader;
using MilkIt.Fluids;

public class TestGrader : MonoBehaviour
{
    public TextMesh drinkFace;
    public TextMesh scoreFace;
    void OnTriggerEnter(Collider other)
    {
        var body = other.attachedRigidbody;
        if (body == null) return;
        var item = body.gameObject;
        GradeDrink(item);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void GradeDrink(GameObject gameObject)
    {
        if (!gameObject.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer)) return;

        (float drinkScore, DrinkType predictedDrink) = DrinkGrader.GradeDrink(fluidContainer);
        string drinkName = predictedDrink.name; // Why are we operating on magic fucking strings for names of drinks................... Love you Claude

        Debug.Log($"Predicted drink {drinkName} with score: {drinkScore}");
        drinkFace.text = drinkName;
        scoreFace.text = $"{(drinkScore * 100f).ToString()}%";
    }
}
