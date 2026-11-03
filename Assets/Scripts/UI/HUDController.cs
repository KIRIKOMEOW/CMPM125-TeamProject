using UnityEngine;
using UnityEngine.UI;

// Read-only HUD: every value comes from GameEvents, nothing here changes game state.
public class HUDController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthText;

    [Header("Blades")]
    [SerializeField] private Image[] bladeIcons = new Image[0];
    [SerializeField] private Color bladeReadyColor = Color.white;
    [SerializeField] private Color bladeOutColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Image recallFill;
    [SerializeField] private Text recallText;

    [Header("Wave")]
    [SerializeField] private Text waveText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text enemiesText;
    [SerializeField] private Text coinsText;

    [Header("Banner")]
    [SerializeField] private Text bannerText;
    [SerializeField] private float bannerDuration = 1.6f;

    [Header("Hurt Overlay")]
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private float hurtFadeTime = 0.45f;

    private float bannerUntil;
    private float hurtAlpha;

    private void OnEnable()
    {
        GameEvents.PlayerHealthChanged += OnHealthChanged;
        GameEvents.BladesChanged += OnBladesChanged;
        GameEvents.RecallCooldownChanged += OnRecallChanged;
        GameEvents.CoinsChanged += OnCoinsChanged;
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveCleared += OnWaveCleared;
        GameEvents.EnemiesRemainingChanged += OnEnemiesChanged;
        GameEvents.WaveTimerChanged += OnTimerChanged;
        GameEvents.PlayerHit += OnPlayerHit;

        // The HUD is hidden in menus, so pull a fresh snapshot whenever it reappears.
        if (GameManager.Instance != null)
        {
            OnCoinsChanged(GameManager.Instance.Coins);
            if (GameManager.Instance.Player != null)
                GameManager.Instance.Player.PublishStatus();
        }
    }

    private void OnDisable()
    {
        GameEvents.PlayerHealthChanged -= OnHealthChanged;
        GameEvents.BladesChanged -= OnBladesChanged;
        GameEvents.RecallCooldownChanged -= OnRecallChanged;
        GameEvents.CoinsChanged -= OnCoinsChanged;
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveCleared -= OnWaveCleared;
        GameEvents.EnemiesRemainingChanged -= OnEnemiesChanged;
        GameEvents.WaveTimerChanged -= OnTimerChanged;
        GameEvents.PlayerHit -= OnPlayerHit;
        hurtAlpha = 0f;
        ApplyHurtAlpha();
    }

    private void Update()
    {
        // Unscaled so the banner and red edge still fade during hit stop.
        if (bannerText != null && bannerText.enabled)
        {
            float remaining = bannerUntil - Time.unscaledTime;
            if (remaining <= 0f)
                bannerText.enabled = false;
            else
            {
                Color color = bannerText.color;
                color.a = Mathf.Clamp01(remaining / 0.4f);
                bannerText.color = color;
            }
        }

        if (hurtAlpha > 0f)
        {
            hurtAlpha = Mathf.Max(0f, hurtAlpha - Time.unscaledDeltaTime / Mathf.Max(0.01f, hurtFadeTime));
            ApplyHurtAlpha();
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (healthFill != null)
            healthFill.fillAmount = max > 0 ? (float)current / max : 0f;
        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }

    private void OnBladesChanged(int inHand, int max)
    {
        for (int i = 0; i < bladeIcons.Length; i++)
        {
            if (bladeIcons[i] == null)
                continue;
            bladeIcons[i].enabled = i < max;
            bladeIcons[i].color = i < inHand ? bladeReadyColor : bladeOutColor;
        }
    }

    private void OnRecallChanged(float remaining, float total)
    {
        if (recallFill != null)
            recallFill.fillAmount = total > 0f ? 1f - remaining / total : 1f;
        if (recallText != null)
            recallText.text = remaining > 0f ? $"RECALL {remaining:0.0}s" : "RECALL READY [E]";
    }

    private void OnCoinsChanged(int total)
    {
        if (coinsText != null)
            coinsText.text = total.ToString();
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        if (waveText != null)
            waveText.text = $"WAVE {wave}/{totalWaves}";
        ShowBanner(wave == totalWaves ? "FINAL WAVE" : $"WAVE {wave}");
    }

    private void OnWaveCleared(int wave)
    {
        ShowBanner(wave >= GameManager.TotalWaves ? "DAWN!" : "WAVE CLEARED");
    }

    private void OnEnemiesChanged(int count)
    {
        if (enemiesText != null)
            enemiesText.text = $"INTRUDERS {count}";
    }

    private void OnTimerChanged(float seconds)
    {
        if (timerText == null)
            return;
        int total = Mathf.FloorToInt(seconds);
        timerText.text = $"{total / 60}:{total % 60:00}";
    }

    private void OnPlayerHit(Vector2 position)
    {
        hurtAlpha = 1f;
        ApplyHurtAlpha();
    }

    private void ShowBanner(string message)
    {
        if (bannerText == null)
            return;
        bannerText.text = message;
        bannerText.enabled = true;
        Color color = bannerText.color;
        color.a = 1f;
        bannerText.color = color;
        bannerUntil = Time.unscaledTime + bannerDuration;
    }

    private void ApplyHurtAlpha()
    {
        if (hurtOverlay == null)
            return;
        Color color = hurtOverlay.color;
        color.a = hurtAlpha * 0.6f;
        hurtOverlay.color = color;
        hurtOverlay.enabled = hurtAlpha > 0f;
    }
}
