using System.Collections.Generic;
using UnityEngine;

namespace MilkIt.NPCs
{
    // Only attached to the desktop test scene. The project may auto-create an XR simulator
    // that expects a VR rig; suspend that runtime instance without changing project settings.
    [DefaultExecutionOrder(-200)]
    public sealed class PrototypeDesktopMode : MonoBehaviour
    {
        readonly List<GameObject> suspended = new List<GameObject>();

        void OnEnable() => SuspendSimulator();
        void Start() => SuspendSimulator();

        void SuspendSimulator()
        {
            // Keep the prototype runnable in a minimal project without an XRI package dependency.
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (component.GetType().FullName !=
                    "UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator") continue;
                var instance = component.gameObject;
                if (!instance.activeSelf) continue;
                suspended.Add(instance);
                instance.SetActive(false);
            }
        }

        void OnDisable()
        {
            foreach (var instance in suspended)
                if (instance != null) instance.SetActive(true);
            suspended.Clear();
        }
    }
}
