using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace MilkIt.Audio
{
    public class VolumeSlider : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;

        [Tooltip("Exactly the name of the exposed parameter in the mixer, e.g. MusicVolume.")]
        [SerializeField] string exposedParameter = "MasterVolume";

        [SerializeField] Slider slider;

        [Tooltip("Optional. Shows e.g. 'Music: 80%'.")]
        [SerializeField] TMP_Text label;
        [SerializeField] string labelName = "Master";

        [SerializeField, Range(0f, 1f)] float defaultValue = 1f;

        string PrefKey => "Settings.Volume." + exposedParameter;

        void Start()
        {
            if (mixer == null || slider == null)
            {
                Debug.LogWarning("VolumeSlider: assign the mixer and the slider.", this);
                enabled = false;
                return;
            }

            float value = PlayerPrefs.GetFloat(PrefKey, defaultValue);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(SetVolume);
            Apply(value);
        }

        void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(SetVolume);
        }

        public void SetVolume(float value)
        {
            Apply(value);
            PlayerPrefs.SetFloat(PrefKey, value);
            PlayerPrefs.Save();
        }

        void Apply(float value)
        {
            float dB = value > 0.0001f ? 20f * Mathf.Log10(value) : -80f;
            if (!mixer.SetFloat(exposedParameter, dB))
                Debug.LogWarning($"VolumeSlider: the mixer has no exposed parameter called '{exposedParameter}'.", this);

            if (label != null) label.text = $"{labelName}: {Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
