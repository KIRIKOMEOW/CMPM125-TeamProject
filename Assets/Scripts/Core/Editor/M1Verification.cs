using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M1Verification
{
    private const string PendingKey = "NightShift.M1Verification.Pending";
    private static IEnumerator checks;
    private static double deadline;
    private static string runtimeError;
    private static double beginAfter;

    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("M1 verification is a batch-only entry point; it exits the editor on completion.");
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Run M1 verification from Edit Mode.");

        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += ScheduleBegin;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            ScheduleBegin();
    }

    private static void ScheduleBegin()
    {
        if (!SessionState.GetBool(PendingKey, false))
            return;

        // Let editor startup/indexing finish before creating the isolated runtime fixture.
        beginAfter = EditorApplication.timeSinceStartup + 1d;
        EditorApplication.update -= WaitForStartup;
        EditorApplication.update += WaitForStartup;
    }

    private static void WaitForStartup()
    {
        if (EditorApplication.timeSinceStartup < beginAfter)
            return;

        EditorApplication.update -= WaitForStartup;
        Begin();
    }

    private static void Begin()
    {
        if (!SessionState.GetBool(PendingKey, false) || checks != null)
            return;

        SessionState.SetBool(PendingKey, false);
        runtimeError = null;
        Debug.Log("M1_VERIFY START: editor startup finished; creating runtime-only test objects.");
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 45d;
        checks = Verify();
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            runtimeError = message;
    }

    private static void Tick()
    {
        try
        {
            Assert(EditorApplication.timeSinceStartup < deadline, "Verification timed out.");
            Assert(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (!checks.MoveNext())
                Finish(null);
        }
        catch (Exception exception)
        {
            Finish(exception);
        }
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        (checks as IDisposable)?.Dispose();
        checks = null;
        if (exception == null)
            Debug.Log("M1_VERIFY PASS: pool, health, state/reset, wave delay/shop/victory, enemy death, blade piercing/recall/root/cooldown/walls.");
        else
            Debug.LogError("M1_VERIFY FAIL: " + exception);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static IEnumerator Verify()
    {
        // Changes exist only in Play Mode memory; no scene, prefab or asset is saved.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);

        PoolManager pool = new GameObject("Verification Pool").AddComponent<PoolManager>();
        GameObject playerObject = new GameObject("Verification Player");
        playerObject.SetActive(false);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        playerObject.AddComponent<CircleCollider2D>().radius = 0.3f;
        GameObject bladeTemplate = new GameObject("Verification Blade Template");
        bladeTemplate.SetActive(false);
        bladeTemplate.AddComponent<Blade>();
        bladeTemplate.GetComponent<CircleCollider2D>().radius = 0.1f;
        SetField(bladeTemplate.GetComponent<Blade>(), "wallLayers", (LayerMask)(1 << 31));
        SetField(player, "bladePrefab", bladeTemplate);
        SetField(player, "throwInterval", 0f);
        playerObject.SetActive(true);
        GameManager manager = new GameObject("Verification GameManager").AddComponent<GameManager>();
        int initialFrame = Time.frameCount;
        while (Time.frameCount == initialFrame)
            yield return null;

        Assert(manager.State == GameState.MainMenu && Time.timeScale == 0f, "Initial menu must pause time.");
        manager.StartGame();
        Assert(manager.State == GameState.Playing && manager.CurrentWave == 1 && Time.timeScale == 1f, "Start wave 1.");
        Assert(player.Health.MaxHP == 100 && player.Health.CurrentHP == 100, "Player starts at 100 HP.");
        manager.PauseGame();
        Assert(manager.State == GameState.Paused && Time.timeScale == 0f, "Pause state.");
        Assert(!player.TryThrow(), "Pause must reject throwing.");
        manager.ResumeGame();
        Assert(manager.State == GameState.Playing && Time.timeScale == 1f, "Resume state.");

        GameObject template = new GameObject("Verification Pool Template");
        template.SetActive(false);
        pool.Prewarm(template, 2);
        GameObject first = pool.Get(template, new Vector2(50f, 50f), Quaternion.identity);
        GameObject second = pool.Get(template, Vector2.zero, Quaternion.identity);
        Assert(first != second && first.activeSelf && second.activeSelf, "Pool leases must be distinct and active.");
        Assert((Vector2)first.transform.position == new Vector2(50f, 50f), "Pool spawn position.");
        pool.Return(first);
        pool.Return(first);
        GameObject reused = pool.Get(template, Vector2.zero, Quaternion.identity);
        GameObject fourth = pool.Get(template, Vector2.zero, Quaternion.identity);
        Assert(reused == first && fourth != reused, "Double return must not duplicate a queued instance.");
        pool.ReturnAll();
        Assert(!first.activeSelf && !second.activeSelf && !fourth.activeSelf, "ReturnAll must retire all leases.");

        DamageInfo hit = new DamageInfo { amount = 10 };
        Assert(player.Health.ApplyDamage(hit), "First player hit accepted.");
        Assert(!player.Health.ApplyDamage(hit) && player.Health.CurrentHP == 90, "Player iframe blocks a repeated hit.");
        float until = Time.time + 0.65f;
        while (Time.time < until)
            yield return null;
        Assert(player.Health.ApplyDamage(hit) && player.Health.CurrentHP == 80, "Player iframe expires after 0.6 seconds.");
        player.Health.Heal(50);
        Assert(player.Health.CurrentHP == 100, "Heal clamps to maximum HP.");
        player.Health.ResetHealth(100);
        Assert(!player.Health.IsInvulnerable, "Health reset clears iframes.");

        GameObject enemyTemplate = new GameObject("Verification Enemy Template");
        enemyTemplate.SetActive(false);
        RusherAI enemyPrototype = enemyTemplate.AddComponent<RusherAI>();
        enemyTemplate.AddComponent<CircleCollider2D>().radius = 0.4f;
        SetField(enemyPrototype, "baseMoveSpeed", 0f);
        SetField(enemyPrototype, "knockbackMultiplier", 0f);
        RusherAI enemy = pool.Get(enemyTemplate, new Vector2(20f, 0f), Quaternion.identity).GetComponent<RusherAI>();
        int deaths = 0;
        Action<Vector2, EnemyType> onDeath = (position, type) => deaths++;
        GameEvents.EnemyKilled += onDeath;
        try
        {
            enemy.TakeDamage(new DamageInfo { amount = 25 });
            Assert(enemy.Health.CurrentHP == 25, "Rusher starts at 50 HP without player iframes.");
            enemy.TakeDamage(new DamageInfo { amount = 25 });
            enemy.TakeDamage(new DamageInfo { amount = 25 });
            Assert(deaths == 1 && !enemy.gameObject.activeSelf, "Enemy dies once and returns to its pool.");
        }
        finally
        {
            GameEvents.EnemyKilled -= onDeath;
        }

        int oldSpawnId = enemy.SpawnId;
        enemy = pool.Get(enemyTemplate, new Vector2(3f, 0f), Quaternion.identity).GetComponent<RusherAI>();
        Assert(enemy.SpawnId == oldSpawnId + 1 && enemy.Health.CurrentHP == 50, "Enemy reuse starts a fresh life.");
        enemy.Health.ResetHealth(200);
        Assert(player.TryThrow() && player.TryThrow() && player.TryThrow(), "Three available blades throw successfully.");
        Assert(player.BladesInHand == 2, "Throwing consumes blades.");
        until = Time.time + 0.75f;
        while (Time.time < until)
            yield return null;
        Assert(enemy.Health.CurrentHP == 125, "Each outbound blade deals 25 damage once while piercing.");
        BladeRecall recall = player.GetComponent<BladeRecall>();
        Assert(recall.TryRecall(), "Recall starts with active blades.");
        Assert(!recall.TryRecall(), "Cooldown rejects a second recall.");
        until = Time.time + 1f;
        while (!enemy.IsRooted && Time.time < until)
            yield return null;
        Assert(enemy.IsRooted && enemy.Health.CurrentHP == 50, "Three recalled blades each hit once and root the enemy.");
        until = Time.time + 0.8f;
        while (player.BladesInHand < player.MaxBlades && Time.time < until)
            yield return null;
        Assert(player.BladesInHand == 5 && enemy.Health.CurrentHP == 50, "All blades return without duplicate damage/refills.");
        manager.PauseGame();
        int pausedFrame = Time.frameCount;
        while (Time.frameCount == pausedFrame)
            yield return null;
        float pausedCooldown = recall.CooldownRemaining;
        double realUntil = EditorApplication.timeSinceStartup + 0.2d;
        while (EditorApplication.timeSinceStartup < realUntil)
            yield return null;
        Assert(Mathf.Approximately(recall.CooldownRemaining, pausedCooldown), "Recall cooldown pauses with gameplay.");
        manager.ResumeGame();
        until = Time.time + 1.05f;
        while (Time.time < until)
            yield return null;
        Assert(!enemy.IsRooted, "Recall root expires after one second.");
        while (recall.CooldownRemaining > 0f)
            yield return null;
        Assert(recall.CooldownRemaining == 0f && recall.Cooldown == 4f, "Recall cooldown ends at zero with four-second base.");

        GameObject wall = new GameObject("Verification Thin Wall");
        wall.layer = 31;
        wall.transform.position = new Vector3(0.25f, 0f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.05f, 3f);
        Physics2D.SyncTransforms();
        Assert(player.TryThrow(), "Throw beside a thin wall.");
        until = Time.time + 0.2f;
        while (Time.time < until)
            yield return null;
        Blade[] stuckBlades = UnityEngine.Object.FindObjectsByType<Blade>();
        Assert(stuckBlades.Length == 1 && stuckBlades[0].transform.position.x < 0.25f
            && enemy.Health.CurrentHP == 50, "A wall between player and muzzle stops the outbound blade.");

        var sceneHandle = SceneManager.GetActiveScene().handle;
        manager.AddCoins(50);
        Assert(manager.TrySpendCoins(20) && manager.Coins == 30 && !manager.TrySpendCoins(31), "Coin spending is atomic.");
        Assert(player.TryThrow() && recall.TryRecall(), "Reset test has an active blade and recall.");
        player.TakeDamage(new DamageInfo { amount = 1000 });
        Assert(manager.State == GameState.GameOver && Time.timeScale == 0f, "Player death enters Game Over.");
        manager.RestartGame();
        Assert(manager.State == GameState.MainMenu && manager.CurrentWave == 0 && manager.Coins == 0, "Restart resets run state.");
        Assert(player.Health.CurrentHP == 100 && player.BladesInHand == 5 && recall.CooldownRemaining == 0f, "Restart resets player/ammo/cooldown.");
        Assert(!enemy.gameObject.activeSelf && UnityEngine.Object.FindObjectsByType<Blade>().Length == 0, "Restart retires enemies and blades.");
        Assert(SceneManager.GetActiveScene().handle == sceneHandle, "Restart keeps the same scene.");
        IEnumerator stateChecks = VerifyWaveStates(manager);
        try
        {
            while (stateChecks.MoveNext())
                yield return stateChecks.Current;
        }
        finally
        {
            (stateChecks as IDisposable)?.Dispose();
        }
        Assert(runtimeError == null, "No unexpected runtime errors.");
    }

    private static IEnumerator VerifyWaveStates(GameManager manager)
    {
        int clearedCount = 0;
        Action<int> onCleared = wave => clearedCount++;
        GameEvents.WaveCleared += onCleared;
        try
        {
            manager.StartGame();
            manager.CompleteWave(2);
            Assert(clearedCount == 0, "An invalid wave completion is ignored.");
            float clearStartedAt = Time.time;
            manager.CompleteWave(1);
            manager.CompleteWave(1);
            Assert(clearedCount == 1, "Wave completion is emitted once.");
            manager.PauseGame();
            double realUntil = EditorApplication.timeSinceStartup + 0.25d;
            while (EditorApplication.timeSinceStartup < realUntil)
                yield return null;
            Assert(manager.State == GameState.Paused && Time.timeScale == 0f, "Wave clear delay respects pause.");
            manager.ResumeGame();
            float until = Time.time + 4f;
            while (manager.State == GameState.Playing && Time.time < until)
                yield return null;
            Assert(manager.State == GameState.Shop && Time.timeScale == 0f
                && Time.time - clearStartedAt >= 3f, "Wave 1 enters Shop only after the three-second gameplay delay.");
            manager.CompleteWave(1);
            Assert(clearedCount == 1, "Shop rejects duplicate wave completion.");
            manager.CloseShopAndStartNextWave();
            SetField(manager, "waveClearDelay", 0.01f);
            for (int wave = 2; wave <= GameManager.TotalWaves; wave++)
            {
                Assert(manager.State == GameState.Playing && manager.CurrentWave == wave && Time.timeScale == 1f,
                    "Shop continue starts the next wave.");
                manager.CompleteWave(wave);
                manager.CompleteWave(wave);
                until = Time.time + 1f;
                while (manager.State == GameState.Playing && Time.time < until)
                    yield return null;
                GameState expected = wave == GameManager.TotalWaves ? GameState.Victory : GameState.Shop;
                Assert(manager.State == expected && Time.timeScale == 0f && clearedCount == wave,
                    "Cleared waves enter Shop; wave 10 enters Victory exactly once.");
                manager.CloseShopAndStartNextWave();
            }
            Assert(manager.State == GameState.Victory && manager.CurrentWave == 10, "Victory cannot start wave 11.");
            manager.ReturnToMainMenu();
            Assert(manager.State == GameState.MainMenu && Time.timeScale == 0f, "Return to menu freezes the world.");
        }
        finally
        {
            GameEvents.WaveCleared -= onCleared;
        }
    }

    private static void SetField(object target, string name, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
