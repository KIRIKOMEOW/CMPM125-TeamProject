using UnityEngine;
using UnityEngine.UI;

// Options overlay: master volume plus the two presentation toggles used when recording the video.
public class OptionsController : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValueText;
    [SerializeField] private Toggle feelToggle;
    [SerializeField] private Toggle debugToggle;

    private bool syncing;

    private void Awake()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (feelToggle != null)
            feelToggle.onValueChanged.AddListener(on => { if (!syncing) GameFeel.Enabled = on; });
        if (debugToggle != null)
            debugToggle.onValueChanged.AddListener(on => { if (!syncing) DebugOverlay.Enabled = on; });
    }

    private void OnEnable()
    {
        syncing = true;
        float volume = AudioManager.Instance != null ? AudioManager.Instance.MasterVolume : AudioListener.volume;
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(volume);
        UpdateVolumeLabel(volume);
        if (feelToggle != null)
            feelToggle.SetIsOnWithoutNotify(GameFeel.Enabled);
        if (debugToggle != null)
            debugToggle.SetIsOnWithoutNotify(DebugOverlay.Enabled);
        syncing = false;
    }

    private void OnVolumeChanged(float value)
    {
        if (syncing)
            return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.MasterVolume = value;
        else
            AudioListener.volume = value;
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
