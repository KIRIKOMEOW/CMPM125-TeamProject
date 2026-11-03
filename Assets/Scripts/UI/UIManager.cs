using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows exactly one menu panel per GameState and wires every menu button.
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Main Menu")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button menuOptionsButton;
    [SerializeField] private Button quitButton;

    [Header("Pause")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseOptionsButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Options")]
    [SerializeField] private Button optionsBackButton;

    [Header("Results")]
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button gameOverMenuButton;
    [SerializeField] private Button victoryRetryButton;
    [SerializeField] private Button victoryMenuButton;
    [SerializeField] private Text gameOverDetailText;
    [SerializeField] private Text victoryDetailText;

    private int coinsEarned;
    private int enemiesKilled;
    private float runTime;

    private void Awake()
    {
        Bind(startButton, () => Manager()?.StartGame());
        Bind(menuOptionsButton, () => ShowOptions(true));
        Bind(quitButton, () => Manager()?.QuitGame());
        Bind(resumeButton, () => Manager()?.ResumeGame());
        Bind(pauseOptionsButton, () => ShowOptions(true));
        Bind(pauseMenuButton, () => Manager()?.ReturnToMainMenu());
        Bind(optionsBackButton, () => ShowOptions(false));
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
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.RunReset -= OnRunReset;
    }

    private void Start()
    {
        OnGameStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            runTime += Time.deltaTime;

        // Esc also backs out of the options overlay.
        if (optionsPanel != null && optionsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            ShowOptions(false);
    }

    private void OnGameStateChanged(GameState state)
    {
        SetActive(optionsPanel, false);
        SetActive(hudPanel, state == GameState.Playing || state == GameState.Paused || state == GameState.Shop);
        SetActive(mainMenuPanel, state == GameState.MainMenu);
        SetActive(pausePanel, state == GameState.Paused);
        SetActive(shopPanel, state == GameState.Shop);
        SetActive(gameOverPanel, state == GameState.GameOver);
        SetActive(victoryPanel, state == GameState.Victory);

        int wave = GameManager.Instance != null ? GameManager.Instance.CurrentWave : 0;
        if (state == GameState.GameOver && gameOverDetailText != null)
            gameOverDetailText.text = $"You held out until wave {wave}.\n{enemiesKilled} intruders stopped · {coinsEarned} coins · {FormatTime(runTime)}";
        if (state == GameState.Victory && victoryDetailText != null)
            victoryDetailText.text = $"The sun is up. The warehouse is safe.\n{enemiesKilled} intruders stopped · {coinsEarned} coins · {FormatTime(runTime)}";

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void ShowOptions(bool show)
    {
        if (optionsPanel == null)
            return;

        GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
        optionsPanel.SetActive(show);
        SetActive(mainMenuPanel, !show && state == GameState.MainMenu);
        SetActive(pausePanel, !show && state == GameState.Paused);
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
