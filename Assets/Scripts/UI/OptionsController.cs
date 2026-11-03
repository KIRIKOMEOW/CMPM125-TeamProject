using UnityEngine;
using UnityEngine.UI;

// Options overlay: volume, presentation toggles, tutorial hints and language.
public class OptionsController : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValueText;
    [SerializeField] private Toggle feelToggle;
    [SerializeField] private Toggle debugToggle;
    [SerializeField] private Toggle tutorialToggle;
    [SerializeField] private Button englishButton;
    [SerializeField] private Button chineseButton;
    [SerializeField] private Color selectedColor = new Color(1f, 0.63f, 0.24f);
    [SerializeField] private Color unselectedColor = new Color(0.55f, 0.6f, 0.75f);

    private bool syncing;

    private void Awake()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (feelToggle != null)
            feelToggle.onValueChanged.AddListener(on => { if (!syncing) GameFeel.Enabled = on; });
        if (debugToggle != null)
            debugToggle.onValueChanged.AddListener(on => { if (!syncing) DebugOverlay.Enabled = on; });
        if (tutorialToggle != null)
            tutorialToggle.onValueChanged.AddListener(on => { if (!syncing) TutorialHints.Enabled = on; });
        if (englishButton != null)
            englishButton.onClick.AddListener(() => SetLanguage(Language.English));
        if (chineseButton != null)
            chineseButton.onClick.AddListener(() => SetLanguage(Language.Chinese));
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
        if (tutorialToggle != null)
            tutorialToggle.SetIsOnWithoutNotify(TutorialHints.Enabled);
        syncing = false;
        RefreshLanguageButtons();
    }

    private void SetLanguage(Language language)
    {
        AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f);
        Loc.Current = language;
        RefreshLanguageButtons();
    }

    private void RefreshLanguageButtons()
    {
        Tint(englishButton, Loc.Current == Language.English);
        Tint(chineseButton, Loc.Current == Language.Chinese);
    }

    private void Tint(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = selected ? selectedColor : unselectedColor;
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
