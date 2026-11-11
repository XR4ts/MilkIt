using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public class WristMenuController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] GameObject wristMenuCanvas;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform head;
    [SerializeField] Button testButton;

    [Header("Placement")]
    [Tooltip("World-space offset from the left controller, in meters.")]
    [SerializeField] Vector3 offsetFromHand = new Vector3(0f, 0.12f, 0f);
    [SerializeField] bool startOpen = true;

    [Header("Test UI")]
    [SerializeField] TMP_Text testButtonLabel;
    [SerializeField] TMP_Text debugLabel;
    [SerializeField] bool showPressedButtons = true;

    InputAction toggleAction;
    IDisposable anyButtonListener;
    bool isOpen;
    int clickCount;

    void Awake()
    {
        toggleAction = new InputAction("Toggle Wrist Menu", InputActionType.Button);
        toggleAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        toggleAction.AddBinding("<XRController>{RightHand}/{MenuButton}");
        toggleAction.AddBinding("<Gamepad>/start");
        toggleAction.AddBinding("<Gamepad>/select");
        toggleAction.AddBinding("<Keyboard>/m");
        SetOpen(startOpen);
    }

    void OnEnable()
    {
        toggleAction.performed += OnTogglePerformed;
        toggleAction.Enable();
        if (showPressedButtons)
            anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        if (testButton != null) testButton.onClick.AddListener(OnTestButtonClicked);
    }

    void OnDisable()
    {
        toggleAction.performed -= OnTogglePerformed;
        toggleAction.Disable();
        anyButtonListener?.Dispose();
        anyButtonListener = null;
        if (testButton != null) testButton.onClick.RemoveListener(OnTestButtonClicked);
    }

    void OnDestroy() => toggleAction.Dispose();

    void LateUpdate()
    {
        if (!isOpen || leftHand == null || head == null)
            return;

        var menu = wristMenuCanvas.transform;
        menu.position = leftHand.position + offsetFromHand;
        menu.rotation = Quaternion.LookRotation(menu.position - head.position, Vector3.up);
    }

    void OnTogglePerformed(InputAction.CallbackContext _) => ToggleMenu();

    public void ToggleMenu() => SetOpen(!isOpen);

    void SetOpen(bool open)
    {
        isOpen = open;
        if (wristMenuCanvas != null)
            wristMenuCanvas.SetActive(open);
    }

    public void OnTestButtonClicked()
    {
        clickCount++;
        if (testButtonLabel != null)
            testButtonLabel.text = $"Clicked {clickCount}x";
    }

    void OnAnyButtonPressed(InputControl control)
    {
        if (debugLabel == null)
            return;
        var hand = control.device.usages.Count > 0 ? control.device.usages[0].ToString() : "-";
        debugLabel.text = $"{control.device.name} [{hand}]\n{control.name}";
    }
}