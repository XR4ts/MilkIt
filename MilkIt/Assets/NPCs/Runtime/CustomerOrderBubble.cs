using UnityEngine;

namespace MilkIt.NPCs
{
    // Speech bubble above a customer: "?" while waiting at the till, then the ordered drink's picture from the
    // coffee poster with its name. Once seated, the picture moves to the patience indicator instead.
    public sealed class CustomerOrderBubble : MonoBehaviour
    {
        public GameObject panel;
        public Renderer image;
        [Tooltip("Shows \"?\" while waiting at the till (or the name if there is no picture).")]
        public TextMesh label;
        [Tooltip("The drink's name above its picture.")]
        public TextMesh nameLabel;
        public float heightAtTill = 0.5f;
        public float heightSeated = 0.95f;
        [Range(0.2f, 1f)] public float seatedScale = 0.6f;

        Transform follow;
        MaterialPropertyBlock block;
        bool seated;
        float nameSize;
        const float MaxNameChars = 13f;
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        public void Attach(Transform target)
        {
            follow = target;
            block = new MaterialPropertyBlock();
            Hide();
        }

        public void ShowWaitingToOrder()
        {
            seated = false;
            panel.SetActive(true);
            image.gameObject.SetActive(false);
            label.gameObject.SetActive(true);
            label.text = "?";
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
        }

        public void ShowOrder(string drink, Texture icon, bool isSeated)
        {
            seated = isSeated;
            panel.SetActive(true);
            var hasIcon = icon != null;
            image.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                block.SetTexture(BaseMap, icon);
                image.SetPropertyBlock(block);
            }
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(hasIcon);
                nameLabel.text = drink;
                // Long names ("Espresso Macchiato") shrink to fit the card's width.
                if (nameSize <= 0f) nameSize = nameLabel.characterSize;
                nameLabel.characterSize = nameSize * Mathf.Min(1f, MaxNameChars / Mathf.Max(1f, drink.Length));
            }
            // Without a picture, the name goes in the middle instead.
            label.gameObject.SetActive(!hasIcon);
            label.text = drink;
        }

        public void Hide() => panel.SetActive(false);

        void LateUpdate()
        {
            if (follow == null) { Destroy(gameObject); return; }
            transform.position = follow.position + Vector3.up * (seated ? heightSeated : heightAtTill);
            var view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;
            transform.localScale = Vector3.one * (seated ? seatedScale : 1f);
        }
    }
}
