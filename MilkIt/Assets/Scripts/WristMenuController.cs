using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using MilkIt.Fluids;


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
    [SerializeField] TMP_Text pauseButtonText;
    [SerializeField] Button resetButton;
    [SerializeField] Button settingsButton;

    [Header("Settings")]
    [SerializeField] Toggle vignetteToggle;
    [Tooltip("Optional. If empty, the first TunnelingVignetteController in the scene is used.")]
    [SerializeField] TunnelingVignetteController vignette;
    const string VignettePrefKey = "Settings.VignetteEnabled";
    [SerializeField] Toggle smoothTurnToggle;
    [Tooltip("Optional. If empty, all ControllerInputActionManagers in the scene are used.")]
    [SerializeField] ControllerInputActionManager[] controllerManagers;
    const string SmoothTurnPrefKey = "Settings.SmoothTurn";
    [SerializeField] Slider puddleSlider;
    [SerializeField] TMP_Text puddleLabel;
    [Tooltip("Fade-out time used by the 'Vanish' setting, in seconds.")]
    [SerializeField] float vanishFadeDuration = 0.5f;
    const string PuddleModePrefKey = "Settings.PuddleMode";
    const int PuddleVanish = 0, PuddleTimed = 1, PuddleStay = 2;
    FluidSpillManager spillManager;
    float defaultPuddleLifetime, defaultPuddleFadeDuration;

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

        if (vignette == null)
            vignette = FindFirstObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);

        bool vignetteOn = PlayerPrefs.GetInt(VignettePrefKey, 1) == 1;
        if (vignetteToggle) vignetteToggle.SetIsOnWithoutNotify(vignetteOn);
        SetVignetteEnabled(vignetteOn);

        if (controllerManagers == null || controllerManagers.Length == 0)
            controllerManagers = FindObjectsByType<ControllerInputActionManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        bool smoothTurnOn = PlayerPrefs.GetInt(SmoothTurnPrefKey, 0) == 1; // default: snap
        if (smoothTurnToggle) smoothTurnToggle.SetIsOnWithoutNotify(smoothTurnOn);
        SetSmoothTurn(smoothTurnOn);

        spillManager = FluidSpillManager.Instance;
        defaultPuddleLifetime = spillManager.lifetime;
        defaultPuddleFadeDuration = spillManager.fadeDuration;

        int puddleMode = PlayerPrefs.GetInt(PuddleModePrefKey, PuddleTimed);
        if (puddleSlider) puddleSlider.SetValueWithoutNotify(puddleMode);
        SetPuddleMode(puddleMode);
    }

    void OnEnable()
    {
        toggleAction.performed += OnTogglePerformed;
        toggleAction.Enable();

        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resetButton) resetButton.onClick.AddListener(ResetObjects);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        if (backFromSettingsButton) backFromSettingsButton.onClick.AddListener(CloseSettings);
        if (vignetteToggle) vignetteToggle.onValueChanged.AddListener(SetVignetteEnabled);
        if (smoothTurnToggle) smoothTurnToggle.onValueChanged.AddListener(SetSmoothTurn);
        if (puddleSlider) puddleSlider.onValueChanged.AddListener(OnPuddleSliderChanged);
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
        if (vignetteToggle) vignetteToggle.onValueChanged.RemoveListener(SetVignetteEnabled);
        if (smoothTurnToggle) smoothTurnToggle.onValueChanged.RemoveListener(SetSmoothTurn);
        if (puddleSlider) puddleSlider.onValueChanged.RemoveListener(OnPuddleSliderChanged);
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
        if (pauseButtonText != null)
        {
            pauseButtonText.text = isPaused ? "Resume" : "Pause";
        }
        Debug.Log(isPaused ? "Game Paused" : "Game Resumed");
    }

    public void ResetObjects()
    {
        // TODO: object reset logic instead whole scene
        Time.timeScale = 1f;
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

    public void SetVignetteEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(VignettePrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (vignette != null)
            vignette.gameObject.SetActive(enabled);
        else
            Debug.LogWarning("WristMenuController: no TunnelingVignetteController found in scene.");
    }

    public void SetSmoothTurn(bool smooth)
    {
        PlayerPrefs.SetInt(SmoothTurnPrefKey, smooth ? 1 : 0);
        PlayerPrefs.Save();

        if (controllerManagers == null || controllerManagers.Length == 0)
        {
            Debug.LogWarning("WristMenuController: no ControllerInputActionManager found in scene.");
            return;
        }

        foreach (var manager in controllerManagers)
            if (manager) manager.smoothTurnEnabled = smooth;
    }

    void OnPuddleSliderChanged(float value) => SetPuddleMode(Mathf.RoundToInt(value));

    public void SetPuddleMode(int mode)
    {
        mode = Mathf.Clamp(mode, PuddleVanish, PuddleStay);
        PlayerPrefs.SetInt(PuddleModePrefKey, mode);
        PlayerPrefs.Save();

        if (spillManager != null)
        {
            switch (mode)
            {
                case PuddleVanish:
                    spillManager.fadeEnabled = true;
                    spillManager.lifetime = 0f;
                    spillManager.fadeDuration = vanishFadeDuration;
                    break;
                case PuddleTimed:
                    spillManager.fadeEnabled = true;
                    spillManager.lifetime = defaultPuddleLifetime;
                    spillManager.fadeDuration = defaultPuddleFadeDuration;
                    break;
                case PuddleStay:
                    spillManager.fadeEnabled = false;
                    break;
            }
        }

        if (puddleLabel)
            puddleLabel.text = mode switch
            {
                PuddleVanish => "Puddles: Vanish",
                PuddleTimed => $"Puddles: Fade after {defaultPuddleLifetime:0}s",
                _ => "Puddles: Stay",
            };
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