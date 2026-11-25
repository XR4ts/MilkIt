using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilkIt.Interaction
{
    /// <summary>
    /// A button pressed by hovering it with a hand or ray and pulling the trigger.
    /// It cannot be selected, so grip does nothing on it. Lights up while hovered.
    /// </summary>
    public class TriggerButton : XRBaseInteractable
    {
        public UnityEvent onPressed = new UnityEvent();

        [Header("Hover highlight")]
        [Tooltip("Renderer that lights up while hovered. Empty = the renderer on this object.")]
        public Renderer highlightRenderer;
        [Range(0f, 1f), Tooltip("How far the colour is pushed towards white while hovered.")]
        public float highlightAmount = 0.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;

        protected override void Awake()
        {
            base.Awake();
            if (highlightRenderer == null) highlightRenderer = GetComponent<Renderer>();
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

        public void Press() => onPressed.Invoke();

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

            for (int i = 0; i < interactorsHovering.Count; i++)
            {
                if (interactorsHovering[i] is XRBaseInputInteractor input && input.logicalActivateState.wasPerformedThisFrame)
                {
                    Press();
                    return;
                }
            }
        }

        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            SetHighlight(true);
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            if (!isHovered) SetHighlight(false);
        }

        void SetHighlight(bool on)
        {
            if (highlightRenderer == null) return;
            if (!on)
            {
                highlightRenderer.SetPropertyBlock(null);
                return;
            }
            Material mat = highlightRenderer.sharedMaterial;
            if (mat == null || !mat.HasProperty(BaseColorId)) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.Lerp(mat.GetColor(BaseColorId), Color.white, highlightAmount));
            highlightRenderer.SetPropertyBlock(block);
        }
    }
}
