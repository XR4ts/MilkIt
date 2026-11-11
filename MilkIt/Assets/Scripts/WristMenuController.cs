using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WristMenuController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] GameObject wristMenuCanvas;
    [SerializeField] GameObject mainButtonsPanel;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform head;

    [Header("Buttons")]
    [SerializeField] Button pauseButton;
    [SerializeField] Button resetButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button backFromSettingsButton;
    [SerializeField] Button quitButton;

    [Header("Placement")]
    [Tooltip("World-space offset from the left controller, in meters.")]
    [SerializeField] Vector3 offsetFromHand = new Vector3(0f, 0.12f, 0f);
    [SerializeField] bool startOpen = true;

    InputAction toggleAction;
    bool isOpen;
    bool isPaused;

    void Awake()
    {
        if (wristMenuCanvas != null && head != null &&
            wristMenuCanvas.TryGetComponent(out Canvas canvas) &&
            head.TryGetComponent(out Camera cam))
            canvas.worldCamera = cam;

        toggleAction = new InputAction("Toggle Wrist Menu", InputActionType.Button);
        toggleAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        toggleAction.AddBinding("<XRController>{RightHand}/{MenuButton}");
        toggleAction.AddBinding("<Keyboard>/m");
        SetOpen(startOpen);
    }

    void OnEnable()
    {
        toggleAction.performed += OnTogglePerformed;
        toggleAction.Enable();

        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resetButton) resetButton.onClick.AddListener(ResetObjects);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        if (backFromSettingsButton) backFromSettingsButton.onClick.AddListener(CloseSettings);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    void OnDisable()
    {
        toggleAction.performed -= OnTogglePerformed;
        toggleAction.Disable();

        if (pauseButton) pauseButton.onClick.RemoveListener(TogglePause);
        if (resetButton) resetButton.onClick.RemoveListener(ResetObjects);
        if (settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
        if (backFromSettingsButton) backFromSettingsButton.onClick.RemoveListener(CloseSettings);
        if (quitButton) quitButton.onClick.RemoveListener(QuitGame);
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

        // reset panels when closing
        if (!open && settingsPanel)
        {
            settingsPanel.SetActive(false);
            if (mainButtonsPanel) mainButtonsPanel.SetActive(true);
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        Debug.Log(isPaused ? "Game Paused" : "Game Resumed");
    }

    public void ResetObjects()
    {
        // TODO: object reset logic
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OpenSettings()
    {
        if (mainButtonsPanel) mainButtonsPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainButtonsPanel) mainButtonsPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}