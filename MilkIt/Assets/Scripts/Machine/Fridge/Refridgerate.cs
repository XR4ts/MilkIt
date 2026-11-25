using UnityEngine;
using MilkIt;
using MilkIt.Fluids;

public class Refridgerate : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer))
            return;
        Debug.Log($"Starting refridgeration on {fluidContainer}");
        fluidContainer.isRefrigerated = true;
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<FluidContainer>(out FluidContainer fluidContainer))
            return;
        Debug.Log($"Stopping refridgeration on {fluidContainer}");
        fluidContainer.isRefrigerated = false;
    }
    
}
