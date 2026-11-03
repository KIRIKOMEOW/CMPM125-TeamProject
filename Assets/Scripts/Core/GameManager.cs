using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.MainMenu;
    public int Coins { get; private set; }
    public int CurrentWave { get; private set; }
    public const int TotalWaves = 10;
    public PlayerController Player => player;

    [Header("Scene References")]
    [SerializeField] private PlayerController player;

    [Header("Wave Timing")]
    [SerializeField] private float waveClearDelay = 3f;

    private bool waveIsClearing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        Time.timeScale = 0f;
    }

    private void OnEnable()
    {
        if (Instance == this)
            GameEvents.PlayerHealthChanged += OnPlayerHealthChanged;
    }

    private void OnDisable()
    {
        GameEvents.PlayerHealthChanged -= OnPlayerHealthChanged;
    }

    private void Start()
    {
        if (Instance != this)
            return;

        GameEvents.RaiseGameStateChanged(State);
        GameEvents.RaiseCoinsChanged(Coins);
        GameEvents.RaiseEnemiesRemainingChanged(0);
        GameEvents.RaiseWaveTimerChanged(0f);
        if (player != null)
            player.PublishStatus();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (State == GameState.Playing)
            PauseGame();
        else if (State == GameState.Paused)
            ResumeGame();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        if (State != GameState.MainMenu)
            return;

        ResetRun();
        BeginWave(1);
    }

    public void PauseGame()
    {
        if (State == GameState.Playing)
            SetState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (State == GameState.Paused)
            SetState(GameState.Playing);
    }

    public void CloseShopAndStartNextWave()
    {
        if (State == GameState.Shop && CurrentWave < TotalWaves)
            BeginWave(CurrentWave + 1);
    }

    public void RestartGame()
    {
        // The design's retry flow returns to the main menu in the same scene.
        ResetRun();
    }

    public void ReturnToMainMenu()
    {
        StopAllCoroutines();
        waveIsClearing = false;
        SetState(GameState.MainMenu);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || amount > Coins)
            return false;

        Coins -= amount;
        GameEvents.RaiseCoinsChanged(Coins);
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        Coins = (int)System.Math.Min((long)Coins + amount, int.MaxValue);
        GameEvents.RaiseCoinsChanged(Coins);
    }

    public void CompleteWave(int wave)
    {
        if (State != GameState.Playing || wave != CurrentWave || waveIsClearing)
            return;

        waveIsClearing = true;
        GameEvents.RaiseWaveCleared(wave);
        StartCoroutine(FinishWave(wave));
    }

    private IEnumerator FinishWave(int wave)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, waveClearDelay));
        while (State == GameState.Paused)
            yield return null;
        if (State != GameState.Playing || CurrentWave != wave)
            yield break;

        waveIsClearing = false;
        SetState(wave >= TotalWaves ? GameState.Victory : GameState.Shop);
    }

    private void BeginWave(int wave)
    {
        CurrentWave = wave;
        waveIsClearing = false;
        GameEvents.RaiseEnemiesRemainingChanged(0);
        GameEvents.RaiseWaveTimerChanged(0f);
        SetState(GameState.Playing);
        if (player != null)
            player.PublishStatus();
        GameEvents.RaiseWaveStarted(CurrentWave, TotalWaves);
    }

    private void ResetRun()
    {
        StopAllCoroutines();
        waveIsClearing = false;
        SetState(GameState.MainMenu);
        GameEvents.RaiseRunReset();
        if (PoolManager.Instance != null)
            PoolManager.Instance.ReturnAll();

        // Also retire any scene-placed test enemies that were not pool-created.
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
        foreach (EnemyBase enemy in enemies)
            enemy.Despawn();

        CurrentWave = 0;
        Coins = 0;
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (player != null)
            player.ResetForNewRun();
        GameEvents.RaiseCoinsChanged(Coins);
        GameEvents.RaiseEnemiesRemainingChanged(0);
        GameEvents.RaiseWaveTimerChanged(0f);
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        if (current > 0 || (State != GameState.Playing && State != GameState.Paused))
            return;

        StopAllCoroutines();
        waveIsClearing = false;
        SetState(GameState.GameOver);
    }

    private void SetState(GameState state)
    {
        State = state;
        Time.timeScale = state == GameState.Playing ? 1f : 0f;
        GameEvents.RaiseGameStateChanged(State);
    }
}
