using UnityEngine;

namespace MilkIt.NPCs
{
    // World-space marker above a customer's head (visible in VR): a cup icon and a patience bar while
    // waiting for a drink, a pulsing "!" when frustrated, and a short popup when paying or giving up.
    public sealed class CustomerPatienceIndicator : MonoBehaviour
    {
        public GameObject waitingGroup;
        public Transform fillPivot;
        public Renderer fillRenderer;
        public TextMesh label;
        public Color calm = new Color(0.3f, 0.85f, 0.35f);
        public Color warning = new Color(1f, 0.8f, 0.2f);
        public Color angry = new Color(0.95f, 0.2f, 0.15f);
        public Color paid = new Color(0.35f, 0.9f, 0.4f);
        public float heightAboveHead = 0.45f;

        Transform follow;
        MaterialPropertyBlock block;
        float popupUntil;
        float patience = 1f;
        bool frustrated;
        bool waiting;
        Vector3 popupOffset;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Attach(Transform target)
        {
            follow = target;
            block = new MaterialPropertyBlock();
            Hide();
        }

        public void ShowWaiting(float remaining01, float secondsLeft, bool isFrustrated)
        {
            waiting = true;
            patience = Mathf.Clamp01(remaining01);
            frustrated = isFrustrated;
            waitingGroup.SetActive(true);
            fillPivot.localScale = new Vector3(Mathf.Max(0.001f, patience), 1f, 1f);
            var color = patience > 0.6f ? calm : Color.Lerp(angry, warning, Mathf.InverseLerp(0f, 0.6f, patience));
            block.SetColor(BaseColor, color);
            fillRenderer.SetPropertyBlock(block);
            if (Time.time >= popupUntil)
            {
                // Negative secondsLeft means unlimited patience: no countdown, only "!" when frustrated.
                label.gameObject.SetActive(frustrated || secondsLeft >= 0f);
                var seconds = secondsLeft >= 0f ? Mathf.CeilToInt(secondsLeft).ToString() : "";
                label.text = frustrated ? ("! " + seconds).Trim() : seconds;
                label.color = frustrated ? angry : Color.white;
            }
        }

        public void ShowPaid(int amount) => Popup("+" + amount, paid);

        public void ShowGaveUp() => Popup("×", angry);

        public void Hide()
        {
            waiting = false;
            frustrated = false;
            waitingGroup.SetActive(false);
            if (Time.time >= popupUntil) label.gameObject.SetActive(false);
        }

        void Popup(string text, Color color)
        {
            waiting = false;
            waitingGroup.SetActive(false);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            popupUntil = Time.time + 2.5f;
            popupOffset = Vector3.zero;
        }

        void LateUpdate()
        {
            if (follow == null) { Destroy(gameObject); return; }
            var position = follow.position + Vector3.up * heightAboveHead;
            if (Time.time < popupUntil)
            {
                popupOffset += Vector3.up * (0.25f * Time.deltaTime);
                position += popupOffset;
            }
            else if (!waiting && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            transform.position = position;

            var view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;

            // Frustrated customers' markers pulse so they stand out across the room.
            var pulse = waiting && frustrated ? 1f + 0.15f * Mathf.Sin(Time.time * 10f) : 1f;
            transform.localScale = Vector3.one * pulse;
        }
    }
}
