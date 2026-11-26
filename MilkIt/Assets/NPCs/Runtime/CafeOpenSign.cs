using MilkIt.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilkIt.NPCs
{
    // OPEN / CLOSED sign hanging on the entrance, like a real shop sign: one side says OPEN, the other CLOSED.
    // Pointing at it (controller ray or hand) and pulling the trigger, or a desktop click, toggles the café's service and turns the board over,
    // so the street side always shows the café's state (and the inside shows the opposite).
    public sealed class CafeOpenSign : MonoBehaviour
    {
        public CafeService service;
        public Transform board;
        [Tooltip("Text on the side facing into the café when the board is unturned (closed).")]
        public TextMesh insideFace;
        [Tooltip("Text on the side facing the street when the board is unturned (closed).")]
        public TextMesh outsideFace;
        public Color openColor = new Color(0.15f, 0.6f, 0.25f);
        public Color closedColor = new Color(0.75f, 0.15f, 0.12f);
        [Min(0.05f)] public float flipSeconds = 0.4f;
        [Tooltip("Optional door panel the sign hangs on; the sign slides with it.")]
        public Transform attachTo;

        Vector3 attachOffset;
        float angle;
        float lastToggle = -1f;
        // One click can arrive twice (e.g. the XR simulator's controller and the mouse helper); count it once.
        const float ToggleCooldown = 0.4f;

        void Awake()
        {
            // The sign in the scene still carries the old XRSimpleInteractable, which reacts to grip.
            var simple = GetComponent<XRSimpleInteractable>();
            if (simple != null)
            {
                simple.enabled = false; // unregisters it now; Destroy only happens at the end of the frame
                Destroy(simple);
            }
            var button = GetComponent<TriggerButton>();
            if (button == null) button = gameObject.AddComponent<TriggerButton>();
            button.onPressed.AddListener(Toggle);
            if (button.highlightRenderer == null && board != null)
            {
                var plate = board.Find("Plate");
                if (plate != null) button.highlightRenderer = plate.GetComponent<Renderer>();
            }
            if (attachTo != null) attachOffset = transform.position - attachTo.position;
            SetFace(insideFace, "OPEN", openColor);
            SetFace(outsideFace, "CLOSED", closedColor);
        }

        void OnEnable()
        {
            // Closed: CLOSED faces the street. Open: the board is turned over, so OPEN faces the street.
            angle = TargetAngle;
            if (board != null) board.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        float TargetAngle => service != null && service.IsOpen ? 180f : 0f;

        public void Toggle()
        {
            if (service == null || Time.unscaledTime - lastToggle < ToggleCooldown) return;
            lastToggle = Time.unscaledTime;
            service.Toggle();
        }

        void Update()
        {
            var target = TargetAngle;
            if (Mathf.Approximately(angle, target)) return;
            angle = Mathf.MoveTowards(angle, target, 180f / flipSeconds * Time.deltaTime);
            board.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        void LateUpdate()
        {
            if (attachTo != null) transform.position = attachTo.position + attachOffset;
            // 3D text ignores depth, so show only the side turned toward the viewer (the other would show
            // through the board mirrored).
            var view = Camera.main;
            if (view == null) return;
            foreach (var face in new[] { insideFace, outsideFace })
            {
                if (face == null) continue;
                var toFace = face.transform.position - view.transform.position;
                face.GetComponent<MeshRenderer>().enabled = Vector3.Dot(face.transform.forward, toFace) > 0f;
            }
        }

        static void SetFace(TextMesh face, string text, Color color)
        {
            if (face == null) return;
            face.text = text;
            face.color = color;
        }
    }
}
