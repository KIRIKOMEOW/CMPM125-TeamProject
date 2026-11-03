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
    [SerializeField] private Color bladeOutColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Image recallFill;
    [SerializeField] private Text recallText;
    [SerializeField] private Color recallReadyColor = new Color(1f, 0.63f, 0.24f);
    [SerializeField] private Color recallCoolingColor = new Color(0.45f, 0.5f, 0.65f);

    [Header("Night Progress")]
    [SerializeField] private Image[] wavePips = new Image[0];
    [SerializeField] private Sprite pipDone;
    [SerializeField] private Sprite pipCurrent;
    [SerializeField] private Sprite pipTodo;
    [SerializeField] private Text waveText;
    [SerializeField] private Text timerText;

    [Header("Counters")]
    [SerializeField] private Text enemiesText;
    [SerializeField] private Text coinsText;

    [Header("Banner")]
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private Text bannerText;
    [SerializeField] private float bannerDuration = 1.6f;

    [Header("Hurt Overlay")]
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private float hurtFadeTime = 0.45f;

    private float bannerUntil;
    private float hurtAlpha;
    private int currentWave;
    private int clearedWave;
    private float recallRemaining;
    private float recallTotal = 1f;

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
        GameEvents.RunReset += OnRunReset;
        Loc.Changed += RefreshText;

        // The HUD is hidden in menus, so pull a fresh snapshot whenever it reappears.
        if (GameManager.Instance != null)
        {
            currentWave = GameManager.Instance.CurrentWave;
            OnCoinsChanged(GameManager.Instance.Coins);
            if (GameManager.Instance.Player != null)
                GameManager.Instance.Player.PublishStatus();
        }
        RefreshPips();
        RefreshText();
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
        GameEvents.RunReset -= OnRunReset;
        Loc.Changed -= RefreshText;
        hurtAlpha = 0f;
        ApplyHurtAlpha();
    }

    private void Update()
    {
        // Unscaled so the banner, red edge and pip pulse still animate during hit stop.
        if (bannerGroup != null && bannerGroup.alpha > 0f)
            bannerGroup.alpha = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / 0.4f);

        if (hurtAlpha > 0f)
        {
            hurtAlpha = Mathf.Max(0f, hurtAlpha - Time.unscaledDeltaTime / Mathf.Max(0.01f, hurtFadeTime));
            ApplyHurtAlpha();
        }

        int index = currentWave - 1;
        if (index >= 0 && index < wavePips.Length && wavePips[index] != null && clearedWave < currentWave)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            wavePips[index].color = new Color(1f, 1f, 1f, pulse);
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
        recallRemaining = remaining;
        recallTotal = total;
        if (recallFill != null)
        {
            recallFill.fillAmount = total > 0f ? 1f - remaining / total : 1f;
            recallFill.color = remaining > 0f ? recallCoolingColor : recallReadyColor;
        }
        RefreshRecallText();
    }

    private void OnCoinsChanged(int total)
    {
        if (coinsText != null)
            coinsText.text = total.ToString();
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        currentWave = wave;
        clearedWave = wave - 1;
        RefreshPips();
        RefreshText();
        ShowBanner(wave == totalWaves ? Loc.Get("hud.banner.final") : Loc.Format("hud.banner.wave", wave));
    }

    private void OnWaveCleared(int wave)
    {
        clearedWave = wave;
        RefreshPips();
        ShowBanner(Loc.Get(wave >= GameManager.TotalWaves ? "hud.banner.dawn" : "hud.banner.cleared"));
    }

    private void OnRunReset()
    {
        currentWave = 0;
        clearedWave = 0;
        RefreshPips();
        RefreshText();
    }

    private void OnEnemiesChanged(int count)
    {
        if (enemiesText != null)
            enemiesText.text = count.ToString();
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

    private void RefreshPips()
    {
        for (int i = 0; i < wavePips.Length; i++)
        {
            if (wavePips[i] == null)
                continue;
            int wave = i + 1;
            wavePips[i].sprite = wave <= clearedWave ? pipDone : wave == currentWave ? pipCurrent : pipTodo;
            wavePips[i].color = Color.white;
        }
    }

    private void RefreshText()
    {
        if (waveText != null)
            waveText.text = Loc.Format("hud.wave", Mathf.Max(1, currentWave));
        RefreshRecallText();
    }

    private void RefreshRecallText()
    {
        if (recallText != null)
            recallText.text = recallRemaining > 0f ? Loc.Format("hud.recall.cd", recallRemaining) : Loc.Get("hud.recall.ready");
    }

    private void ShowBanner(string message)
    {
        if (bannerText != null)
        {
            bannerText.font = Loc.Font;
            bannerText.text = message;
        }
        if (bannerGroup != null)
            bannerGroup.alpha = 1f;
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
