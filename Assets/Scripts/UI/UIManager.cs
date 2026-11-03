using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows exactly one menu panel per GameState, wires every menu button and applies the language font.
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject howToPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Main Menu")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button menuHowToButton;
    [SerializeField] private Button menuOptionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button languageButton;
    [SerializeField] private Text languageButtonText;

    [Header("Pause")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseHowToButton;
    [SerializeField] private Button pauseOptionsButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Overlays")]
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button howToCloseButton;

    [Header("Results")]
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button gameOverMenuButton;
    [SerializeField] private Button victoryRetryButton;
    [SerializeField] private Button victoryMenuButton;
    [SerializeField] private Text gameOverDetailText;
    [SerializeField] private Text victoryDetailText;
    [SerializeField] private Text[] gameOverStats = new Text[4];
    [SerializeField] private Text[] victoryStats = new Text[4];

    private int coinsEarned;
    private int enemiesKilled;
    private float runTime;

    private void Awake()
    {
        Bind(startButton, () => Manager()?.StartGame());
        Bind(menuHowToButton, () => ShowOverlay(howToPanel, true));
        Bind(menuOptionsButton, () => ShowOverlay(optionsPanel, true));
        Bind(quitButton, () => Manager()?.QuitGame());
        Bind(languageButton, () => Loc.Current = Loc.Current == Language.English ? Language.Chinese : Language.English);
        Bind(resumeButton, () => Manager()?.ResumeGame());
        Bind(pauseHowToButton, () => ShowOverlay(howToPanel, true));
        Bind(pauseOptionsButton, () => ShowOverlay(optionsPanel, true));
        Bind(pauseMenuButton, () => Manager()?.ReturnToMainMenu());
        Bind(optionsBackButton, () => ShowOverlay(optionsPanel, false));
        Bind(howToCloseButton, () => ShowOverlay(howToPanel, false));
        Bind(gameOverRetryButton, () => Manager()?.RestartGame());
        Bind(gameOverMenuButton, () => Manager()?.ReturnToMainMenu());
        Bind(victoryRetryButton, () => Manager()?.RestartGame());
        Bind(victoryMenuButton, () => Manager()?.ReturnToMainMenu());
    }

    private void OnEnable()
    {
        GameEvents.GameStateChanged += OnGameStateChanged;
        GameEvents.CoinCollected += OnCoinCollected;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.RunReset += OnRunReset;
        Loc.Changed += OnLanguageChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.RunReset -= OnRunReset;
        Loc.Changed -= OnLanguageChanged;
    }

    private void Start()
    {
        OnLanguageChanged();
        OnGameStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            runTime += Time.deltaTime;

        // Esc backs out of whichever overlay is open.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (optionsPanel != null && optionsPanel.activeSelf) ShowOverlay(optionsPanel, false);
            else if (howToPanel != null && howToPanel.activeSelf) ShowOverlay(howToPanel, false);
        }
    }

    private void OnGameStateChanged(GameState state)
    {
        SetActive(optionsPanel, false);
        SetActive(howToPanel, false);
        SetActive(hudPanel, state == GameState.Playing || state == GameState.Paused || state == GameState.Shop);
        SetActive(mainMenuPanel, state == GameState.MainMenu);
        SetActive(pausePanel, state == GameState.Paused);
        SetActive(shopPanel, state == GameState.Shop);
        SetActive(gameOverPanel, state == GameState.GameOver);
        SetActive(victoryPanel, state == GameState.Victory);

        if (state == GameState.GameOver || state == GameState.Victory)
            RefreshResults();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshResults()
    {
        int wave = GameManager.Instance != null ? GameManager.Instance.CurrentWave : 0;
        if (gameOverDetailText != null)
            gameOverDetailText.text = Loc.Format("over.detail", wave);
        if (victoryDetailText != null)
            victoryDetailText.text = Loc.Get("win.detail");
        FillStats(gameOverStats, wave);
        FillStats(victoryStats, wave);
    }

    private void FillStats(Text[] stats, int wave)
    {
        string[] values = { $"{wave}/{GameManager.TotalWaves}", enemiesKilled.ToString(), coinsEarned.ToString(), FormatTime(runTime) };
        for (int i = 0; i < stats.Length && i < values.Length; i++)
            if (stats[i] != null) stats[i].text = values[i];
    }

    private void ShowOverlay(GameObject overlay, bool show)
    {
        if (overlay == null)
            return;

        GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
        SetActive(optionsPanel, show && overlay == optionsPanel);
        SetActive(howToPanel, show && overlay == howToPanel);
        SetActive(mainMenuPanel, !show && state == GameState.MainMenu);
        SetActive(pausePanel, !show && state == GameState.Paused);
    }

    private void OnLanguageChanged()
    {
        // Dynamic labels are re-filled by their owners; here every Text just gets the right glyphs.
        Font font = Loc.Font;
        foreach (Text text in GetComponentsInChildren<Text>(true))
            text.font = text.GetComponent<LanguageNameLabel>() != null ? Loc.ChineseFont : font;
        if (languageButtonText != null)
            languageButtonText.text = Loc.Current == Language.English ? "中文" : "EN";
        GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
        if (state == GameState.GameOver || state == GameState.Victory)
            RefreshResults();
    }

    private void OnCoinCollected(Vector2 position, int amount) => coinsEarned += amount;
    private void OnEnemyKilled(Vector2 position, EnemyType type) => enemiesKilled++;

    private void OnRunReset()
    {
        coinsEarned = 0;
        enemiesKilled = 0;
        runTime = 0f;
    }

    private void Bind(Button button, System.Action action)
    {
        if (button == null)
            return;

        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f);
            action();
            // Clearing selection stops Space (the recall key) from re-pressing the last button.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        });
    }

    private static GameManager Manager() => GameManager.Instance;

    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }
}
