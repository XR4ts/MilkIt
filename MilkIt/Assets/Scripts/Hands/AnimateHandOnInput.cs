using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class AnimateHandOnInput : MonoBehaviour
{


    [Header("Hand")]
    [SerializeField]
    Transform m_HandTransform;

    [SerializeField]
    Vector2 m_HandRotationRange = new Vector2(30f, 30f);

    [SerializeField]
    XRInputValueReader<Vector2> m_StickInput = new XRInputValueReader<Vector2>("Thumbstick");
    
    public InputActionProperty triggerValue;
    public InputActionProperty gripValue;
    public Animator handAnimator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnEnable()
    {
        if (m_HandTransform == null)
            {
                enabled = false;
                Debug.LogWarning($"Controller Animator component missing references on {gameObject.name}", this);
                return;
            }

            m_StickInput?.EnableDirectActionIfModeUsed();
    }

    void OnDisable()
        {
            m_StickInput?.DisableDirectActionIfModeUsed();
        }
    // Update is called once per frame
    void Update()
    {
        float trigger = triggerValue.action.ReadValue<float>();
        float grip = gripValue.action.ReadValue<float>();
        handAnimator.SetFloat("Trigger", trigger);
        handAnimator.SetFloat("Grip", grip);

        if (m_StickInput != null)
            {
                var stickVal = m_StickInput.ReadValue();
                m_HandTransform.localRotation = Quaternion.Euler(-stickVal.y * m_HandRotationRange.x, 0f, -stickVal.x * m_HandRotationRange.y);
            }
    }
}
