using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaveSpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private EnemyBase rusherPrefab;
    [SerializeField] private EnemyBase guardBotPrefab;
    [SerializeField] private EnemyBase brutePrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [Tooltip("Optional player reference for spawn-distance Gizmos before Play Mode.")]
    [SerializeField] private PlayerController player;
    [SerializeField] private float minimumSpawnDistance = 8f;

    [Header("Bursts")]
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private int enemiesPerBurst = 3;

    private readonly List<EnemyBase> spawnPlan = new List<EnemyBase>();
    private readonly List<EnemyBase> choices = new List<EnemyBase>(3);
    private readonly Dictionary<EnemyBase, int> liveEnemies = new Dictionary<EnemyBase, int>();
    private readonly HashSet<Transform> usedSpawnPoints = new HashSet<Transform>();
    private Coroutine spawnRoutine;
    private int nextSpawn;
    private int unplannedBudget;
    private int waveVersion;
    private bool planReady;

    public int CurrentWave { get; private set; }
    public int WaveBudget { get; private set; }
    public int PendingEnemies => planReady ? spawnPlan.Count - nextSpawn : unplannedBudget;
    public int ActiveEnemies => liveEnemies.Count;
    public int RemainingEnemies => PendingEnemies + ActiveEnemies;
    public float ElapsedTime { get; private set; }
    public bool IsWaveActive { get; private set; }

    private void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.RunReset += OnRunReset;
        EnemyBase.Died += OnEnemyDied;
    }

    private void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.RunReset -= OnRunReset;
        EnemyBase.Died -= OnEnemyDied;
        ClearWave();
    }

    private void Update()
    {
        if (!IsWaveActive || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;

        ElapsedTime += Time.deltaTime;
        GameEvents.RaiseWaveTimerChanged(ElapsedTime);
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        if (wave < 1 || wave > GameManager.TotalWaves)
            return;

        ClearWave();
        CurrentWave = wave;
        WaveBudget = 4 + 3 * wave;
        unplannedBudget = WaveBudget;
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
            player = GameManager.Instance.Player;
        IsWaveActive = true;
        TryBuildPlan();
        PublishRemaining();
        GameEvents.RaiseWaveTimerChanged(0f);
        spawnRoutine = StartCoroutine(SpawnWave(waveVersion));
    }

    private IEnumerator SpawnWave(int version)
    {
        while (IsWaveActive && version == waveVersion)
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                if (TryBuildPlan())
                    SpawnBurst(version);
                TryCompleteWave();
            }

            if (IsWaveActive && version == waveVersion)
                yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));
        }
        if (version == waveVersion)
            spawnRoutine = null;
    }

    private bool TryBuildPlan()
    {
        if (planReady)
            return true;

        // A cost-one enemy is required to spend every budget remainder exactly.
        if (!IsPrefabValid(rusherPrefab, EnemyType.Rusher))
            return false;

        // Each available tier appears after its introduction; randomness fills the remaining budget.
        if (CurrentWave >= 3 && IsPrefabValid(guardBotPrefab, EnemyType.GuardBot))
        {
            spawnPlan.Add(guardBotPrefab);
            unplannedBudget -= 2;
        }
        if (CurrentWave >= 6 && IsPrefabValid(brutePrefab, EnemyType.Brute))
        {
            spawnPlan.Add(brutePrefab);
            unplannedBudget -= 4;
        }

        while (unplannedBudget > 0)
        {
            choices.Clear();
            choices.Add(rusherPrefab);
            if (CurrentWave >= 3 && unplannedBudget >= 2 && IsPrefabValid(guardBotPrefab, EnemyType.GuardBot))
                choices.Add(guardBotPrefab);
            if (CurrentWave >= 6 && unplannedBudget >= 4 && IsPrefabValid(brutePrefab, EnemyType.Brute))
                choices.Add(brutePrefab);

            EnemyBase prefab = choices[Random.Range(0, choices.Count)];
            spawnPlan.Add(prefab);
            unplannedBudget -= GetCost(prefab.Type);
        }

        planReady = true;
        PublishRemaining();
        return true;
    }

    private void SpawnBurst(int version)
    {
        if (PoolManager.Instance == null)
            return;

        usedSpawnPoints.Clear();
        for (int i = 0; i < enemiesPerBurst && nextSpawn < spawnPlan.Count; i++)
        {
            if (version != waveVersion || !IsWaveActive || !TryGetSpawnPosition(out Vector2 position))
                break;

            EnemyBase prefab = spawnPlan[nextSpawn];
            if (prefab == null)
                break;
            GameObject instance = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity);
            if (instance == null)
                break;

            EnemyBase enemy = instance.GetComponent<EnemyBase>();
            if (version != waveVersion || !IsWaveActive || enemy == null)
            {
                PoolManager.Instance.Return(instance);
                break;
            }

            enemy.Initialize(CurrentWave, player);
            liveEnemies[enemy] = enemy.SpawnId;
            nextSpawn++;
            PublishRemaining();
        }
    }

    private bool TryGetSpawnPosition(out Vector2 position)
    {
        position = Vector2.zero;
        if (player == null || spawnPoints == null)
            return false;

        int eligibleCount = 0;
        Transform selectedPoint = null;
        foreach (Transform point in spawnPoints)
        {
            if (usedSpawnPoints.Contains(point) || !IsPointEligible(point, player.transform.position))
                continue;

            eligibleCount++;
            if (Random.Range(0, eligibleCount) == 0)
                selectedPoint = point;
        }
        if (selectedPoint == null)
            return false;

        // Sparse arenas spawn smaller bursts instead of stacking multiple bodies at one point.
        usedSpawnPoints.Add(selectedPoint);
        position = selectedPoint.position;
        return true;
    }

    private bool IsPointEligible(Transform point, Vector2 playerPosition)
    {
        return point != null && point.gameObject.activeInHierarchy
            && ((Vector2)point.position - playerPosition).sqrMagnitude >= minimumSpawnDistance * minimumSpawnDistance;
    }

    private void OnEnemyDied(EnemyBase enemy, int spawnId)
    {
        if (!IsWaveActive || !liveEnemies.TryGetValue(enemy, out int trackedSpawn) || trackedSpawn != spawnId)
            return;

        liveEnemies.Remove(enemy);
        PublishRemaining();
        TryCompleteWave();
    }

    private void TryCompleteWave()
    {
        if (!IsWaveActive || !planReady || RemainingEnemies != 0)
            return;

        IsWaveActive = false;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        GameEvents.RaiseWaveTimerChanged(ElapsedTime);
        if (GameManager.Instance != null)
            GameManager.Instance.CompleteWave(CurrentWave);
    }

    private void OnRunReset()
    {
        ClearWave();
        PublishRemaining();
        GameEvents.RaiseWaveTimerChanged(0f);
    }

    private void ClearWave()
    {
        waveVersion++;
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = null;
        IsWaveActive = false;
        planReady = false;
        spawnPlan.Clear();
        liveEnemies.Clear();
        usedSpawnPoints.Clear();
        nextSpawn = 0;
        unplannedBudget = 0;
        CurrentWave = 0;
        WaveBudget = 0;
        ElapsedTime = 0f;
    }

    private void PublishRemaining()
    {
        GameEvents.RaiseEnemiesRemainingChanged(RemainingEnemies);
    }

    private static bool IsPrefabValid(EnemyBase prefab, EnemyType type)
    {
        return prefab != null && prefab.Type == type;
    }

    private static int GetCost(EnemyType type)
    {
        return type == EnemyType.Brute ? 4 : type == EnemyType.GuardBot ? 2 : 1;
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null)
            return;

        PlayerController currentPlayer = player;
        if (currentPlayer == null && GameManager.Instance != null)
            currentPlayer = GameManager.Instance.Player;
        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            Gizmos.color = currentPlayer != null && IsPointEligible(point, currentPlayer.transform.position)
                ? Color.green : Color.red;
            Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }

    private void OnValidate()
    {
        minimumSpawnDistance = Mathf.Max(0f, minimumSpawnDistance);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        enemiesPerBurst = Mathf.Max(1, enemiesPerBurst);
    }
}
