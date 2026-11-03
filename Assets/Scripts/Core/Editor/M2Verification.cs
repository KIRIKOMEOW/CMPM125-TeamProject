using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M2Verification
{
    private const string PendingKey = "NightShift.M2Verification.Pending";
    private static IEnumerator checks;
    private static double deadline;
    private static string runtimeError;
    private static double beginAfter;

    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("M2 verification is a batch-only entry point; it exits the editor on completion.");
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Run M2 verification from Edit Mode.");

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
        Debug.Log("M2_VERIFY START: editor startup finished; creating runtime-only test objects.");
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 60d;
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
            Debug.Log("M2_VERIFY PASS: upgrades, prices/caps/reset, pickups/adaptive drops, spawn budgets/safety/pause/death counts, tier gates and shop income.");
        else
            Debug.LogError("M2_VERIFY FAIL: " + exception);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static IEnumerator Verify()
    {
        if (AudioManager.Instance != null) UnityEngine.Object.DestroyImmediate(AudioManager.Instance.gameObject);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);

        PoolManager pool = new GameObject("M2 Pool").AddComponent<PoolManager>();
        GameObject playerObject = new GameObject("M2 Player");
        playerObject.SetActive(false);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        playerObject.AddComponent<CircleCollider2D>().radius = 0.3f;
        playerObject.SetActive(true);
        GameManager manager = new GameObject("M2 GameManager").AddComponent<GameManager>();
        SetField(manager, "waveClearDelay", 0.01f);
        UpgradeSystem upgrades = new GameObject("M2 Upgrades").AddComponent<UpgradeSystem>();
        GameObject coinTemplate = PickupTemplate("M2 Coin Template", 0f);
        GameObject healthTemplate = PickupTemplate("M2 Health Template", 0f);
        DropTable drops = new GameObject("M2 DropTable").AddComponent<DropTable>();
        SetField(drops, "coinPrefab", coinTemplate);
        SetField(drops, "healthPrefab", healthTemplate);
        int frame = Time.frameCount;
        while (Time.frameCount == frame)
            yield return null;

        manager.StartGame();
        manager.AddCoins(1000);
        Assert(!upgrades.CanBuy(UpgradeType.ThrowRate) && !upgrades.TryBuy(UpgradeType.ThrowRate),
            "Upgrades cannot be purchased during gameplay.");
        manager.CompleteWave(1);
        while (manager.State == GameState.Playing)
            yield return null;
        Assert(manager.State == GameState.Shop, "Upgrade fixture enters the shop.");
        VerifyUpgrades(manager, player, upgrades);
        manager.ReturnToMainMenu();
        foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            Assert(upgrades.GetLevel(type) == 0, "RunReset clears every upgrade level.");
        Assert(Approximately(player.MoveSpeed, 6f) && player.BladeDamage == 25 && Approximately(player.ThrowInterval, 0.3f)
            && player.Health.MaxHP == 100 && player.Health.CurrentHP == 100 && player.MaxBlades == 5
            && !player.FanThrowUnlocked && manager.Coins == 0, "RunReset restores all player baselines.");

        manager.StartGame();
        VerifyDrops(pool, manager, player, drops, coinTemplate, healthTemplate);
        Pickup magnet = pool.Get(coinTemplate, new Vector2(3f, 0f), Quaternion.identity).GetComponent<Pickup>();
        SetField(magnet, "magnetRange", 12f);
        SetField(magnet, "magnetSpeed", 12f);
        magnet.Initialize(PickupType.Coin, 7, player);
        int coinsBefore = manager.Coins;
        manager.PauseGame();
        Vector2 pausedPosition = magnet.transform.position;
        double realUntil = EditorApplication.timeSinceStartup + 0.1d;
        while (EditorApplication.timeSinceStartup < realUntil)
            yield return null;
        Assert((Vector2)magnet.transform.position == pausedPosition && manager.Coins == coinsBefore,
            "Coin attraction and collection pause with gameplay.");
        manager.ResumeGame();
        float until = Time.time + 1.5f;
        while (magnet.gameObject.activeSelf && Time.time < until)
            yield return null;
        Assert(!magnet.gameObject.activeSelf && manager.Coins == coinsBefore + 7, "A nearby coin attracts and collects exactly once.");

        Pickup distantCoin = pool.Get(coinTemplate, new Vector2(100f, 0f), Quaternion.identity).GetComponent<Pickup>();
        distantCoin.Initialize(PickupType.Coin, 9, player);
        coinsBefore = manager.Coins;
        manager.CompleteWave(1);
        while (manager.State == GameState.Playing)
            yield return null;
        Assert(manager.Coins == coinsBefore + 9 && !distantCoin.gameObject.activeSelf,
            "Uncollected coins settle before the paused shop can strand them.");
        manager.ReturnToMainMenu();

        // No colliders are needed for spawn policy tests; avoiding overlap keeps spawn positions stable.
        RusherAI rusher = EnemyTemplate("M2 Rusher", EnemyType.Rusher);
        RusherAI guard = EnemyTemplate("M2 Guard Cost Fixture", EnemyType.GuardBot);
        RusherAI brute = EnemyTemplate("M2 Brute Cost Fixture", EnemyType.Brute);
        Transform point = new GameObject("M2 Spawn Point").transform;
        point.position = new Vector2(7f, 0f);
        Transform secondPoint = new GameObject("M2 Second Spawn Point").transform;
        secondPoint.position = new Vector2(-7f, 0f);
        Transform thirdPoint = new GameObject("M2 Third Spawn Point").transform;
        thirdPoint.position = new Vector2(0f, 7f);
        WaveSpawner spawner = new GameObject("M2 WaveSpawner").AddComponent<WaveSpawner>();
        SetField(spawner, "rusherPrefab", rusher);
        SetField(spawner, "guardBotPrefab", guard);
        SetField(spawner, "brutePrefab", brute);
        SetField(spawner, "spawnPoints", new[] { point, secondPoint, thirdPoint });
        SetField(drops, "extraPickupChance", 0f);
        manager.StartGame();
        Assert(spawner.WaveBudget == 7 && spawner.RemainingEnemies == 7 && spawner.PendingEnemies == 7
            && spawner.ActiveEnemies == 0, "Wave 1 budget is seven and unsafe points cannot spend it.");
        VerifyPlan(spawner, 1);
        frame = Time.frameCount;
        while (Time.frameCount == frame)
            yield return null;
        manager.PauseGame();
        float pausedTimer = spawner.ElapsedTime;
        point.position = new Vector2(8f, 0f);
        secondPoint.position = new Vector2(-8f, 0f);
        thirdPoint.position = new Vector2(0f, 8f);
        realUntil = EditorApplication.timeSinceStartup + 0.15d;
        while (EditorApplication.timeSinceStartup < realUntil)
            yield return null;
        Assert(Approximately(spawner.ElapsedTime, pausedTimer) && spawner.ActiveEnemies == 0,
            "Wave timer and spawning freeze during pause even after a point becomes safe.");
        manager.ResumeGame();
        until = Time.time + 2f;
        while (spawner.ActiveEnemies == 0 && Time.time < until)
            yield return null;
        Assert(spawner.ActiveEnemies == 3 && spawner.PendingEnemies == 4 && spawner.RemainingEnemies == 7,
            "The first valid burst creates three enemies without dropping pending counts.");
        foreach (EnemyBase enemy in UnityEngine.Object.FindObjectsByType<EnemyBase>())
            Assert(Vector2.Distance(enemy.transform.position, player.transform.position) >= 7.999f,
                "Every spawn is at least eight units from the player.");
        float firstBurstAt = Time.time;
        EnemyBase untracked = pool.Get(rusher.gameObject, new Vector2(20f, 0f), Quaternion.identity).GetComponent<EnemyBase>();
        untracked.TakeDamage(new DamageInfo { amount = 10000 });
        Assert(spawner.RemainingEnemies == 7, "A death outside this wave's tracked leases cannot change its count.");
        KillActiveEnemies();
        Assert(spawner.ActiveEnemies == 0 && spawner.PendingEnemies == 4 && spawner.RemainingEnemies == 4
            && manager.State == GameState.Playing && spawner.IsWaveActive, "Killing a burst cannot clear unspawned budget.");
        until = Time.time + 2f;
        while (spawner.ActiveEnemies == 0 && Time.time < until)
            yield return null;
        Assert(spawner.ActiveEnemies == 3 && Time.time - firstBurstAt >= 1.45f,
            "Successive spawn bursts honor the 1.5-second interval.");
        KillActiveEnemies();
        until = Time.time + 2f;
        while (spawner.ActiveEnemies == 0 && Time.time < until)
            yield return null;
        Assert(spawner.ActiveEnemies == 1 && spawner.PendingEnemies == 0 && spawner.RemainingEnemies == 1,
            "The final wave-1 burst spends the last budget point exactly.");
        KillActiveEnemies();
        while (manager.State == GameState.Playing)
            yield return null;
        Assert(manager.State == GameState.Shop && spawner.RemainingEnemies == 0 && !spawner.IsWaveActive
            && manager.Coins == 24, "Seven tracked kills and one unrelated kill each reward three gold once before Shop.");
        Assert(upgrades.CanBuy(UpgradeType.MoveSpeed), "Wave-one income can afford the first movement upgrade.");

        SetField(spawner, "spawnInterval", 0.02f);
        manager.CloseShopAndStartNextWave();
        Assert(spawner.CurrentWave == 2 && spawner.WaveBudget == 10 && spawner.RemainingEnemies == 10,
            "Wave 2 starts with ten total enemies including pending spawns.");
        Assert(Approximately(drops.CurrentCoinMultiplier, 1.5f), "A no-damage wave grants 50 percent more coins next wave.");
        VerifyPlan(spawner, 2);
        player.TakeDamage(new DamageInfo { amount = 10 });
        while (manager.State == GameState.Playing)
        {
            KillActiveEnemies();
            yield return null;
        }
        Assert(manager.State == GameState.Shop && manager.Coins == 64,
            "Wave 2 rounds boosted Rusher rewards to four coins without duplicate death/drop counting.");
        manager.CloseShopAndStartNextWave();
        Assert(Approximately(drops.CurrentCoinMultiplier, 1f), "Taking damage removes the next wave's coin bonus.");

        for (int wave = 3; wave <= 6; wave++)
        {
            VerifyPlan(spawner, wave);
            if (wave == 6)
                break;
            while (manager.State == GameState.Playing)
            {
                KillActiveEnemies();
                yield return null;
            }
            manager.CloseShopAndStartNextWave();
        }
        manager.ReturnToMainMenu();
        Assert(!spawner.IsWaveActive && spawner.CurrentWave == 0 && spawner.PendingEnemies == 0
            && spawner.ActiveEnemies == 0 && spawner.ElapsedTime == 0f && drops.CurrentCoinMultiplier == 1f,
            "Menu reset cancels the wave, live/pending counts, timer and adaptive bonus.");
        Assert(UnityEngine.Object.FindObjectsByType<EnemyBase>().Length == 0
            && UnityEngine.Object.FindObjectsByType<Pickup>().Length == 0 && manager.Coins == 0,
            "Menu reset returns all enemies and pickups without awarding abandoned coins.");
        realUntil = EditorApplication.timeSinceStartup + 0.1d;
        while (EditorApplication.timeSinceStartup < realUntil)
            yield return null;
        Assert(spawner.RemainingEnemies == 0 && manager.State == GameState.MainMenu, "A cancelled spawn coroutine cannot revive the run.");
        SetField(spawner, "rusherPrefab", null);
        manager.StartGame();
        until = Time.time + 0.1f;
        while (Time.time < until)
            yield return null;
        Assert(spawner.PendingEnemies == 7 && spawner.ActiveEnemies == 0 && manager.State == GameState.Playing,
            "A missing mandatory prefab waits safely instead of clearing a free wave.");
        manager.ReturnToMainMenu();

        SetField(spawner, "rusherPrefab", rusher);
        SetField(spawner, "spawnPoints", new[] { point, secondPoint, thirdPoint });
        manager.StartGame();
        EnemyBase[] burst = UnityEngine.Object.FindObjectsByType<EnemyBase>();
        Assert(burst.Length == 3 && spawner.PendingEnemies == 4 && spawner.RemainingEnemies == 7,
            "A separated first burst preserves its full wave budget.");
        for (int i = 0; i < burst.Length; i++)
        {
            Assert(Vector2.Distance(burst[i].transform.position, player.transform.position) >= 7.999f,
                "Distinct spawn-point selection retains the eight-unit safety boundary.");
            for (int j = i + 1; j < burst.Length; j++)
                Assert(Vector2.Distance(burst[i].transform.position, burst[j].transform.position) > 0.01f,
                    "A burst uses distinct spawn points when enough safe points are available.");
        }
        manager.ReturnToMainMenu();
        SetField(spawner, "spawnPoints", new[] { point, point });
        manager.StartGame();
        Assert(spawner.ActiveEnemies == 1 && spawner.PendingEnemies == 6 && spawner.RemainingEnemies == 7,
            "A single unique safe point caps the burst instead of overlapping enemies or discarding budget.");
        manager.ReturnToMainMenu();
    }

    private static void VerifyUpgrades(GameManager manager, PlayerController player, UpgradeSystem upgrades)
    {
        int purchases = 0;
        int coinEvents = 0;
        int lastHealth = 0;
        int lastMax = 0;
        Action<UpgradeType, int> purchased = (type, level) => purchases++;
        Action<int> coinsChanged = value => coinEvents++;
        Action<int, int> healthChanged = (current, max) => { lastHealth = current; lastMax = max; };
        GameEvents.UpgradePurchased += purchased;
        GameEvents.CoinsChanged += coinsChanged;
        GameEvents.PlayerHealthChanged += healthChanged;
        try
        {
            int[] ratePrices = { 20, 35, 55 };
            int[] damagePrices = { 25, 40, 60 };
            int[] hpPrices = { 20, 35, 55 };
            int[] speedPrices = { 15, 30 };
            for (int level = 1; level <= 3; level++)
            {
                Buy(manager, upgrades, UpgradeType.ThrowRate, ratePrices[level - 1], level);
                Assert(Approximately(player.ThrowInterval, 0.3f / (1f + 0.15f * level)), "Throw rate uses additive 15 percent per level.");
            }
            player.Health.ApplyDamage(new DamageInfo { amount = 10 });
            for (int level = 1; level <= 3; level++)
            {
                Buy(manager, upgrades, UpgradeType.Damage, damagePrices[level - 1], level);
                Assert(player.BladeDamage == 25 + 10 * level && player.Health.CurrentHP == 90,
                    "Damage upgrades add ten and do not heal unrelated damage.");
            }
            for (int level = 1; level <= 3; level++)
            {
                player.Health.ResetHealth(player.Health.MaxHP);
                player.Health.ApplyDamage(new DamageInfo { amount = 20 });
                Buy(manager, upgrades, UpgradeType.MaxHP, hpPrices[level - 1], level);
                int expected = 100 + 25 * level;
                Assert(player.Health.CurrentHP == expected && player.Health.MaxHP == expected
                    && lastHealth == expected && lastMax == expected, "MaxHP adds twenty-five, fully heals and publishes the HUD.");
            }
            for (int level = 1; level <= 2; level++)
            {
                Buy(manager, upgrades, UpgradeType.MoveSpeed, speedPrices[level - 1], level);
                Assert(Approximately(player.MoveSpeed, 6f * (1f + 0.1f * level)), "Move speed adds ten percent per level.");
            }
            foreach (UpgradeType type in new[] { UpgradeType.ThrowRate, UpgradeType.Damage, UpgradeType.MaxHP, UpgradeType.MoveSpeed })
                Assert(upgrades.GetPrice(type) == -1 && !upgrades.CanBuy(type) && !upgrades.TryBuy(type), "Capped upgrades cannot charge coins.");
            Assert(upgrades.GetMaxLevel(UpgradeType.Heal) == -1 && upgrades.GetPrice(UpgradeType.Heal) == 15
                && !upgrades.CanBuy(UpgradeType.Heal), "Heal is unlimited but cannot be bought at full HP.");
            player.Health.ResetHealth(175);
            player.Health.ApplyDamage(new DamageInfo { amount = 60 });
            Buy(manager, upgrades, UpgradeType.Heal, 15, 1);
            Assert(player.Health.CurrentHP == 165, "Heal restores fifty HP.");
            Buy(manager, upgrades, UpgradeType.Heal, 15, 2);
            Assert(player.Health.CurrentHP == 175 && !upgrades.TryBuy(UpgradeType.Heal), "Heal caps at maximum and then rejects purchase.");
            Assert(upgrades.GetPrice(UpgradeType.FanThrow) == 80 && upgrades.GetMaxLevel(UpgradeType.FanThrow) == 1
                && !upgrades.CanBuy(UpgradeType.FanThrow) && !upgrades.TryBuy(UpgradeType.FanThrow)
                && !player.FanThrowAvailable && !player.FanThrowUnlocked, "Deferred fan throw cannot charge for an unavailable ability.");
            Assert(purchases == 13 && coinEvents == 13, "Every successful purchase emits one purchase event and one coin change.");
            player.Health.ResetHealth(175);
            player.Health.ApplyDamage(new DamageInfo { amount = 60 });
            manager.TrySpendCoins(manager.Coins);
            int before = purchases;
            Assert(!upgrades.CanBuy(UpgradeType.Heal) && !upgrades.TryBuy(UpgradeType.Heal)
                && purchases == before && manager.Coins == 0, "Insufficient funds do not mutate upgrades or coins.");
        }
        finally
        {
            GameEvents.UpgradePurchased -= purchased;
            GameEvents.CoinsChanged -= coinsChanged;
            GameEvents.PlayerHealthChanged -= healthChanged;
        }
    }

    private static void VerifyDrops(PoolManager pool, GameManager manager, PlayerController player,
        DropTable drops, GameObject coinTemplate, GameObject healthTemplate)
    {
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        try
        {
            SetField(drops, "extraPickupChance", 0f);
            EnemyType[] types = { EnemyType.Rusher, EnemyType.GuardBot, EnemyType.Brute };
            int[] amounts = { 3, 4, 10 };
            for (int i = 0; i < types.Length; i++)
            {
                drops.Drop(new Vector2(100f, 0f), types[i]);
                Pickup[] active = UnityEngine.Object.FindObjectsByType<Pickup>();
                Assert(active.Length == 1 && active[0].Type == PickupType.Coin && active[0].Amount == amounts[i],
                    "Each enemy type drops its configured guaranteed coins.");
                int before = manager.Coins;
                Assert(active[0].TryCollect(player) && !active[0].TryCollect(player) && manager.Coins == before + amounts[i],
                    "A pickup can credit its value only once per pool lease.");
            }
            Pickup health = pool.Get(healthTemplate, new Vector2(100f, 0f), Quaternion.identity).GetComponent<Pickup>();
            health.Initialize(PickupType.Health, 25, player);
            Assert(!health.TryCollect(player) && health.gameObject.activeSelf, "A health pack is retained at full HP.");
            player.Health.ResetHealth(100);
            player.Health.ApplyDamage(new DamageInfo { amount = 60 });
            Assert(health.TryCollect(player) && player.Health.CurrentHP == 65 && !health.gameObject.activeSelf,
                "Health packs heal and return to their pool.");
            SetField(drops, "extraPickupChance", 0.15f);
            player.Health.ResetHealth(100);
            int noExtraSeed = FindSeed((chance, choice) => chance >= 0.15f && chance < 0.16f);
            UnityEngine.Random.InitState(noExtraSeed);
            drops.Drop(new Vector2(100f, 0f), EnemyType.Rusher);
            Assert(UnityEngine.Object.FindObjectsByType<Pickup>().Length == 1, "A roll immediately above fifteen percent has no extra pickup.");
            pool.ReturnAll();
            int adaptiveSeed = FindSeed((chance, choice) => chance > 0.14f && chance < 0.15f && choice > 0.5f && choice < 0.75f);
            player.Health.ResetHealth(100);
            player.Health.ApplyDamage(new DamageInfo { amount = 60 });
            UnityEngine.Random.InitState(adaptiveSeed);
            drops.Drop(new Vector2(100f, 0f), EnemyType.Rusher);
            Pickup[] normal = UnityEngine.Object.FindObjectsByType<Pickup>();
            Assert(normal.Length == 2 && CountType(normal, PickupType.Health) == 0,
                "At exactly forty percent HP, a middle-weight extra roll selects coins.");
            pool.ReturnAll();
            player.Health.ResetHealth(100);
            player.Health.ApplyDamage(new DamageInfo { amount = 61 });
            UnityEngine.Random.InitState(adaptiveSeed);
            drops.Drop(new Vector2(100f, 0f), EnemyType.Rusher);
            Pickup[] adaptive = UnityEngine.Object.FindObjectsByType<Pickup>();
            Assert(adaptive.Length == 2 && CountType(adaptive, PickupType.Health) == 1,
                "Below forty percent, the same extra roll selects health with threefold weight.");
            pool.ReturnAll();
            SetField(drops, "extraPickupChance", 0f);
            SetField(drops, "coinPrefab", null);
            int fallbackBefore = manager.Coins;
            drops.Drop(new Vector2(100f, 0f), EnemyType.Rusher);
            Assert(manager.Coins == fallbackBefore + 3, "Missing coin art cannot break guaranteed shop income.");
            SetField(drops, "coinPrefab", coinTemplate);
            player.Health.ResetHealth(100);
        }
        finally
        {
            UnityEngine.Random.state = previousRandom;
        }
    }

    private static void Buy(GameManager manager, UpgradeSystem upgrades, UpgradeType type, int price, int level)
    {
        int before = manager.Coins;
        Assert(upgrades.GetPrice(type) == price && upgrades.CanBuy(type) && upgrades.TryBuy(type)
            && upgrades.GetLevel(type) == level && manager.Coins == before - price, "Price, level and payment must match for " + type + ".");
    }

    private static void VerifyPlan(WaveSpawner spawner, int wave)
    {
        IList plan = (IList)GetField(spawner, "spawnPlan");
        int cost = 0;
        int guards = 0;
        int brutes = 0;
        foreach (EnemyBase enemy in plan)
        {
            if (enemy.Type == EnemyType.GuardBot) guards++;
            if (enemy.Type == EnemyType.Brute) brutes++;
            cost += enemy.Type == EnemyType.Brute ? 4 : enemy.Type == EnemyType.GuardBot ? 2 : 1;
        }
        Assert(cost == 4 + 3 * wave && spawner.WaveBudget == cost, "Spawn plan spends the exact wave budget.");
        Assert(wave >= 3 ? guards >= 1 : guards == 0, "Guard tier begins at wave three when configured.");
        Assert(wave >= 6 ? brutes >= 1 : brutes == 0, "Brute tier begins at wave six when configured.");
    }

    private static void KillActiveEnemies()
    {
        foreach (EnemyBase enemy in UnityEngine.Object.FindObjectsByType<EnemyBase>())
        {
            enemy.TakeDamage(new DamageInfo { amount = 10000 });
            enemy.TakeDamage(new DamageInfo { amount = 10000 });
        }
    }

    private static GameObject PickupTemplate(string name, float range)
    {
        GameObject template = new GameObject(name);
        template.SetActive(false);
        Pickup pickup = template.AddComponent<Pickup>();
        SetField(pickup, "magnetRange", range);
        return template;
    }

    private static RusherAI EnemyTemplate(string name, EnemyType type)
    {
        GameObject template = new GameObject(name);
        template.SetActive(false);
        RusherAI enemy = template.AddComponent<RusherAI>();
        SetField(enemy, "enemyType", type);
        SetField(enemy, "baseMoveSpeed", 0f);
        return enemy;
    }

    private static int FindSeed(Func<float, float, bool> predicate)
    {
        for (int seed = 0; seed < 100000; seed++)
        {
            UnityEngine.Random.InitState(seed);
            if (predicate(UnityEngine.Random.value, UnityEngine.Random.value))
                return seed;
        }
        throw new InvalidOperationException("No deterministic random seed found for the drop boundary test.");
    }

    private static int CountType(Pickup[] pickups, PickupType type)
    {
        int count = 0;
        foreach (Pickup pickup in pickups)
            if (pickup.Type == type) count++;
        return count;
    }

    private static object GetField(object target, string name)
    {
        return Field(target, name).GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        Field(target, name).SetValue(target, value);
    }

    private static FieldInfo Field(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static bool Approximately(float actual, float expected)
    {
        return Mathf.Abs(actual - expected) < 0.0001f;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
