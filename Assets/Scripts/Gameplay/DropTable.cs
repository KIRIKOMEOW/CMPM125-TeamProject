using UnityEngine;

[DisallowMultipleComponent]
public class DropTable : MonoBehaviour
{
    [Header("Pickup Prefabs")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject healthPrefab;

    [Header("Enemy Rewards")]
    [SerializeField] private int rusherCoins = 3;
    [SerializeField] private int guardBotCoins = 4;
    [SerializeField] private int bruteCoins = 10;

    [Header("Extra Pickup")]
    [SerializeField, Range(0f, 1f)] private float extraPickupChance = 0.15f;
    [SerializeField] private float coinWeight = 1f;
    [SerializeField] private float healthWeight = 1f;
    [SerializeField] private int extraCoinAmount = 1;
    [SerializeField] private int healthAmount = 25;

    [Header("Adaptive Drops")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.4f;
    [SerializeField] private float lowHealthWeightMultiplier = 3f;
    [SerializeField] private float perfectWaveCoinMultiplier = 1.5f;

    private int currentWave;
    private int lastClearedWave;
    private bool waveActive;
    private bool playerWasHit;
    private bool nextWaveBonus;

    public static DropTable Instance { get; private set; }
    public float CurrentCoinMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveCleared += OnWaveCleared;
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.RunReset += ResetDrops;
    }

    private void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveCleared -= OnWaveCleared;
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.RunReset -= ResetDrops;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Drop(Vector2 position, EnemyType type)
    {
        GameManager manager = GameManager.Instance;
        if (!isActiveAndEnabled || Instance != this || manager == null || manager.State != GameState.Playing)
            return;

        int baseCoins;
        switch (type)
        {
            case EnemyType.Rusher: baseCoins = rusherCoins; break;
            case EnemyType.GuardBot: baseCoins = guardBotCoins; break;
            case EnemyType.Brute: baseCoins = bruteCoins; break;
            default: return;
        }
        PlayerController player = manager.Player;
        DropCoins(position, Mathf.RoundToInt(baseCoins * CurrentCoinMultiplier), player);
        if (Random.value >= extraPickupChance)
            return;

        float adjustedHealthWeight = HealthWeight(player);
        float totalWeight = Mathf.Max(0f, coinWeight) + adjustedHealthWeight;
        if (totalWeight <= 0f)
            return;

        if (Random.value * totalWeight < adjustedHealthWeight)
            SpawnPickup(healthPrefab, position, PickupType.Health, healthAmount, player);
        else
            DropCoins(position, Mathf.RoundToInt(extraCoinAmount * CurrentCoinMultiplier), player);
    }

    private float HealthWeight(PlayerController player)
    {
        float weight = Mathf.Max(0f, healthWeight);
        if (player != null && player.Health != null && player.Health.CurrentHP < player.Health.MaxHP * lowHealthThreshold)
            weight *= lowHealthWeightMultiplier;
        return weight;
    }

    private void DropCoins(Vector2 position, int amount, PlayerController player)
    {
        if (amount <= 0)
            return;
        // Missing art/prefabs must not prevent the shop economy from functioning.
        if (!SpawnPickup(coinPrefab, position, PickupType.Coin, amount, player))
            GameManager.Instance.AddCoins(amount);
    }

    private bool SpawnPickup(GameObject prefab, Vector2 position, PickupType type, int amount, PlayerController player)
    {
        if (prefab == null || amount <= 0 || PoolManager.Instance == null)
            return false;
        GameObject instance = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
        if (instance == null)
            return false;
        Pickup pickup = instance.GetComponent<Pickup>();
        if (pickup == null)
        {
            PoolManager.Instance.Return(instance);
            return false;
        }
        pickup.Initialize(type, amount, player);
        return true;
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        CurrentCoinMultiplier = wave > 1 && nextWaveBonus && lastClearedWave == wave - 1
            ? perfectWaveCoinMultiplier : 1f;
        currentWave = wave;
        nextWaveBonus = false;
        playerWasHit = false;
        waveActive = true;
    }

    private void OnPlayerHit(Vector2 position)
    {
        if (waveActive && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            playerWasHit = true;
    }

    private void OnWaveCleared(int wave)
    {
        if (!waveActive || wave != currentWave)
            return;
        nextWaveBonus = !playerWasHit;
        lastClearedWave = wave;
        waveActive = false;
    }

    private void ResetDrops()
    {
        currentWave = 0;
        lastClearedWave = 0;
        waveActive = false;
        playerWasHit = false;
        nextWaveBonus = false;
        CurrentCoinMultiplier = 1f;
    }

    private void OnValidate()
    {
        rusherCoins = Mathf.Max(0, rusherCoins);
        guardBotCoins = Mathf.Max(0, guardBotCoins);
        bruteCoins = Mathf.Max(0, bruteCoins);
        coinWeight = Mathf.Max(0f, coinWeight);
        healthWeight = Mathf.Max(0f, healthWeight);
        extraCoinAmount = Mathf.Max(1, extraCoinAmount);
        healthAmount = Mathf.Max(1, healthAmount);
        lowHealthWeightMultiplier = Mathf.Max(0f, lowHealthWeightMultiplier);
        perfectWaveCoinMultiplier = Mathf.Max(1f, perfectWaveCoinMultiplier);
    }
}
