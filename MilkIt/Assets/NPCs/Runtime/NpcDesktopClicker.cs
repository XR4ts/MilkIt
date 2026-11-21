using UnityEngine;
using UnityEngine.InputSystem;

namespace MilkIt.NPCs
{
    // Desktop/simulator testing: left-click a customer at the till to take their order, or the sign to open or
    // close. In the headset the same objects are selected with the controller ray or by poking them.
    public sealed class NpcDesktopClicker : MonoBehaviour
    {
        public CafeCustomerSystem cafe;
        [Min(1f)] public float maxDistance = 30f;

        void Update()
        {
            var mouse = Mouse.current;
            var view = Camera.main;
            if (mouse == null || view == null || !mouse.leftButton.wasPressedThisFrame) return;
            var ray = view.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)) return;
            var customer = hit.collider.GetComponentInParent<CustomerController>();
            if (customer != null && cafe != null) { cafe.TakeOrder(customer); return; }
            var sign = hit.collider.GetComponentInParent<CafeOpenSign>();
            if (sign != null) sign.Toggle();
        }
    }
}
