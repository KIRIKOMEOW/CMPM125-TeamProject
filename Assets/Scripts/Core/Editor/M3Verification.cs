using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M3Verification
{
    private const string PendingKey = "NightShift.M3Verification.Pending";
    private const int PropLayer = 31;
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static double beginAfter;
    private static double deadline;
    private static string runtimeError;

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying)
            throw new InvalidOperationException("Run M3 verification in a batch editor starting from Edit Mode.");
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
        if (state == PlayModeStateChange.EnteredPlayMode) ScheduleBegin();
    }

    private static void ScheduleBegin()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        beginAfter = EditorApplication.timeSinceStartup + 1d;
        EditorApplication.update -= WaitForStartup;
        EditorApplication.update += WaitForStartup;
    }

    private static void WaitForStartup()
    {
        if (EditorApplication.timeSinceStartup < beginAfter) return;
        EditorApplication.update -= WaitForStartup;
        if (!SessionState.GetBool(PendingKey, false) || routines.Count > 0) return;
        SessionState.SetBool(PendingKey, false);
        runtimeError = null;
        Debug.Log("M3_VERIFY START: editor startup finished; creating runtime-only test objects.");
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 60d;
        routines.Push(Verify());
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
            while (routines.Count > 0)
            {
                IEnumerator current = routines.Peek();
                if (!current.MoveNext())
                {
                    (routines.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                return;
            }
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
        while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        if (exception == null)
            Debug.Log("M3_VERIFY PASS: Guard perception/states/shooting, projectile cover/pooling, blade impulses/anchors, crate reset, barrel damage/force/chain/respawn.");
        else
            Debug.LogError("M3_VERIFY FAIL: " + exception);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static IEnumerator Verify()
    {
        if (AudioManager.Instance != null) UnityEngine.Object.DestroyImmediate(AudioManager.Instance.gameObject);
        // All setup exists only in Play Mode memory; no scene or prefab asset is saved.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);

        PoolManager pool = new GameObject("M3 Pool").AddComponent<PoolManager>();
        GameObject playerObject = new GameObject("M3 Player");
        playerObject.SetActive(false);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        playerObject.AddComponent<CircleCollider2D>().radius = 0.3f;
        playerObject.SetActive(true);
        GameManager manager = new GameObject("M3 GameManager").AddComponent<GameManager>();
        SetField(manager, "waveClearDelay", 0.01f);
        yield return Frames(1);
        manager.StartGame();

        GameObject projectileTemplate = new GameObject("M3 Projectile Template");
        projectileTemplate.SetActive(false);
        EnemyProjectile projectile = projectileTemplate.AddComponent<EnemyProjectile>();
        projectileTemplate.GetComponent<CircleCollider2D>().radius = 0.1f;
        SetField(projectile, "blockingLayers", (LayerMask)(1 << PropLayer));
        GameObject guardTemplate = new GameObject("M3 Guard Template");
        guardTemplate.SetActive(false);
        GuardBotAI guard = guardTemplate.AddComponent<GuardBotAI>();
        guardTemplate.AddComponent<CircleCollider2D>().radius = 0.4f;
        SetField(guard, "baseMoveSpeed", 0f);
        SetField(guard, "sightBlockingLayers", (LayerMask)(1 << PropLayer));
        SetField(guard, "projectilePrefab", projectileTemplate);

        yield return VerifyGuard(pool, manager, player, guardTemplate);
        manager.RestartGame();
        yield return VerifyProjectiles(pool, manager, player, guardTemplate, projectileTemplate);
        manager.RestartGame();
        yield return VerifyBladeAndCrate(pool, manager, player);
        manager.RestartGame();
        yield return VerifyBarrels(pool, manager, player);
        manager.ReturnToMainMenu();
        Assert(runtimeError == null, "No runtime errors occurred during M3 checks.");
    }

    private static IEnumerator VerifyGuard(PoolManager pool, GameManager manager, PlayerController player, GameObject template)
    {
        GuardBotAI guard = pool.Get(template, new Vector2(4f, 0f), Quaternion.identity).GetComponent<GuardBotAI>();
        Assert(guard.Type == EnemyType.GuardBot && guard.Health.CurrentHP == 75 && guard.Health.MaxHP == 75,
            "A new Guard has its own type and seventy-five HP without Inspector overrides.");
        yield return Until(() => guard.CurrentStateName == "Attack", 0.5f, "A visible player within six units enters Attack.");
        Assert(guard.HasLineOfSight, "An unobstructed Guard sees its target.");
        yield return Until(() => player.Health.CurrentHP < 100, 2f, "An attacking Guard fires its configured pooled projectile.");
        Assert(player.Health.CurrentHP == 90, "The Guard's projectile deals ten damage once.");
        SetField(guard, "projectilePrefab", null);
        foreach (EnemyProjectile shot in UnityEngine.Object.FindObjectsByType<EnemyProjectile>()) pool.Return(shot.gameObject);
        player.ResetForNewRun();
        guard.GetComponent<Rigidbody2D>().position = new Vector2(9f, 0f);
        Physics2D.SyncTransforms();
        yield return Until(() => guard.CurrentStateName == "Chase", 0.5f, "A visible player beyond attack range enters Chase.");

        GameObject cover = Solid("M3 Guard Cover", new Vector2(4.5f, 0f), new Vector2(0.3f, 3f));
        yield return Until(() => guard.CurrentStateName == "Search", 0.5f, "Solid cover blocks sight and starts Search.");
        Assert(!guard.HasLineOfSight, "A covered player is not treated as visible.");
        manager.PauseGame();
        yield return Realtime(0.1d);
        Assert(guard.CurrentStateName == "Search", "Pause preserves the search state.");
        manager.ResumeGame();
        player.GetComponent<Rigidbody2D>().position = new Vector2(100f, 0f);
        guard.GetComponent<Rigidbody2D>().position = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return GameTime(3.1f);
        Assert(guard.CurrentStateName == "Patrol", "The three-second search ends in Patrol when the player remains unseen.");
        pool.Return(guard.gameObject);
        guard = pool.Get(template, new Vector2(20f, 0f), Quaternion.identity).GetComponent<GuardBotAI>();
        Assert(guard.CurrentStateName == "Patrol" && !guard.HasLineOfSight && guard.Health.CurrentHP == 75,
            "A pooled Guard resets perception, state and health.");
        UnityEngine.Object.DestroyImmediate(cover);
    }

    private static IEnumerator VerifyProjectiles(PoolManager pool, GameManager manager, PlayerController player,
        GameObject guardTemplate, GameObject projectileTemplate)
    {
        GuardBotAI owner = pool.Get(guardTemplate, new Vector2(10f, 0f), Quaternion.identity).GetComponent<GuardBotAI>();
        SetField(owner, "projectilePrefab", null);
        GameObject cover = Solid("M3 Projectile Cover", new Vector2(5f, 0f), new Vector2(0.3f, 3f));
        EnemyProjectile shot = Shot(pool, projectileTemplate, owner, Vector2.left);
        yield return Until(() => !shot.gameObject.activeSelf, 1f, "A shot stops and returns to its pool on solid cover.");
        Assert(player.Health.CurrentHP == 100, "Cover prevents projectile damage behind it.");

        // A muzzle placed beyond the same cover must not bypass the center-to-muzzle sweep.
        shot = pool.Get(projectileTemplate, new Vector2(4f, 0f), Quaternion.identity).GetComponent<EnemyProjectile>();
        shot.Launch(Vector2.left, owner);
        Assert(!shot.gameObject.activeSelf && player.Health.CurrentHP == 100, "A muzzle offset cannot shoot through cover.");
        UnityEngine.Object.DestroyImmediate(cover);

        GameObject friendTemplate = new GameObject("M3 Friendly Enemy Template");
        friendTemplate.SetActive(false);
        RusherAI friendPrototype = friendTemplate.AddComponent<RusherAI>();
        friendTemplate.AddComponent<CircleCollider2D>().radius = 0.4f;
        SetField(friendPrototype, "baseMoveSpeed", 0f);
        EnemyBase friend = pool.Get(friendTemplate, new Vector2(5f, 0f), Quaternion.identity).GetComponent<EnemyBase>();
        shot = Shot(pool, projectileTemplate, owner, Vector2.left);
        manager.PauseGame();
        Vector2 pausedPosition = shot.transform.position;
        yield return Realtime(0.1d);
        Assert(shot.gameObject.activeSelf && (Vector2)shot.transform.position == pausedPosition,
            "Projectile flight and lifetime pause with gameplay.");
        manager.ResumeGame();
        yield return Until(() => !shot.gameObject.activeSelf, 1.5f, "An unobstructed projectile reaches the player.");
        Assert(friend.Health.CurrentHP == 50 && player.Health.CurrentHP == 90,
            "Enemy projectiles pass friendly enemies and hurt only the player once.");

        shot = pool.Get(projectileTemplate, owner.transform.position, Quaternion.identity).GetComponent<EnemyProjectile>();
        SetField(shot, "lifetime", 0.05f);
        shot.Launch(Vector2.right, owner);
        yield return Until(() => !shot.gameObject.activeSelf, 0.3f, "An expired projectile is recycled.");
        shot = Shot(pool, projectileTemplate, owner, Vector2.right);
        manager.RestartGame();
        Assert(!shot.gameObject.activeSelf && UnityEngine.Object.FindObjectsByType<EnemyProjectile>().Length == 0,
            "Restart returns every enemy projectile without waiting for its lifetime.");
    }

    private static IEnumerator VerifyBladeAndCrate(PoolManager pool, GameManager manager, PlayerController player)
    {
        GameObject crateObject = new GameObject("M3 Blade Crate");
        crateObject.SetActive(false);
        crateObject.layer = PropLayer;
        crateObject.transform.position = new Vector2(2f, 0f);
        Crate crate = crateObject.AddComponent<Crate>();
        crateObject.SetActive(true);
        Rigidbody2D crateBody = crateObject.GetComponent<Rigidbody2D>();
        Assert(crateBody.bodyType == RigidbodyType2D.Dynamic && Approximately(crateBody.mass, 2f)
            && Approximately(crateBody.linearDamping, 5f), "Crates have the configured pushable physics defaults.");
        crateBody.linearDamping = 0f;
        ChildCollider(crateObject, "M3 Compound Crate Collider").gameObject.layer = PropLayer;
        GameObject bladeTemplate = new GameObject("M3 Blade Template");
        bladeTemplate.SetActive(false);
        Blade prototype = bladeTemplate.AddComponent<Blade>();
        bladeTemplate.GetComponent<CircleCollider2D>().radius = 0.1f;
        SetField(prototype, "wallLayers", (LayerMask)(1 << PropLayer));
        SetField(prototype, "hitLayers", (LayerMask)0);
        SetField(player, "bladePrefab", bladeTemplate);
        Physics2D.SyncTransforms();
        Assert(player.TryThrow(), "A blade can be thrown toward a crate.");
        Blade blade = UnityEngine.Object.FindAnyObjectByType<Blade>();
        yield return Until(() => blade.IsResting, 0.5f, "An outbound blade stops on the crate's wall layer.");
        Assert(blade.StuckBody == crateBody && !blade.GetComponent<Rigidbody2D>().simulated && crateBody.linearVelocity.x > 0f,
            "A non-damageable crate is pushed even when it is excluded from damage layers, and the blade anchors to it.");
        Assert(crateBody.linearVelocity.x < 2.1f, "Compound crate colliders do not multiply the blade impulse.");
        Vector2 previousBlade = blade.transform.position;
        Vector2 previousCrate = crateBody.position;
        crateBody.linearVelocity = Vector2.zero;
        crateBody.position += new Vector2(1f, 0.5f);
        Physics2D.SyncTransforms();
        yield return Frames(2);
        Assert(Vector2.Distance((Vector2)blade.transform.position - previousBlade, crateBody.position - previousCrate) < 0.03f,
            "A resting blade follows a moved crate without blocking its physics.");

        player.GetComponent<Rigidbody2D>().position = new Vector2(6f, 0.5f);
        Physics2D.SyncTransforms();
        BladeRecall recall = player.GetComponent<BladeRecall>();
        Assert(recall.TryRecall() && blade.IsReturning && blade.StuckBody == null && blade.GetComponent<Rigidbody2D>().simulated,
            "Recall detaches the resting blade and restores flight simulation.");
        yield return Until(() => player.BladesInHand == player.MaxBlades, 1f, "A recalled blade passes through its crate and returns.");
        Assert(crateBody.linearVelocity.x > 0f && crateBody.linearVelocity.x < 2.1f,
            "Returning through a compound crate applies one additional impulse for the return phase.");
        manager.RestartGame();
        Assert(Vector2.Distance(crateBody.position, new Vector2(2f, 0f)) < 0.001f && crateBody.linearVelocity == Vector2.zero,
            "Run reset restores a crate's initial pose and clears its velocity.");

        crateBody.position = new Vector2(20f, 0f);
        Physics2D.SyncTransforms();
        Assert(player.TryThrow(), "The recycled blade can launch again.");
        blade = UnityEngine.Object.FindAnyObjectByType<Blade>();
        Assert(!blade.IsResting && blade.StuckBody == null && blade.GetComponent<Rigidbody2D>().simulated,
            "Pool reuse has no stale attachment or disabled flight simulation.");
        manager.RestartGame();
        Assert(player.TryThrow(), "A second crate attachment can be created.");
        blade = UnityEngine.Object.FindAnyObjectByType<Blade>();
        yield return Until(() => blade.IsResting, 0.5f, "The second throw attaches to the reset crate.");
        crateObject.SetActive(false);
        yield return Frames(2);
        Assert(blade.isActiveAndEnabled && blade.CanRecall && blade.StuckBody == null,
            "Disabling the attachment target leaves the blade available for recall.");
        Assert(recall.TryRecall(), "A blade remains recallable after its target disappears.");
        yield return Until(() => player.BladesInHand == player.MaxBlades, 1f, "The detached blade restores the final inventory slot.");
        UnityEngine.Object.DestroyImmediate(crateObject);
    }

    private static IEnumerator VerifyBarrels(PoolManager pool, GameManager manager, PlayerController player)
    {
        ExplosiveBarrel barrel = Barrel("M3 Barrel", new Vector2(10f, 0f));
        Rigidbody2D crate = BareBody("M3 Explosion Body", new Vector2(11.5f, 0f));
        ChildCollider(crate.gameObject, "M3 Explosion Compound Collider");
        Rigidbody2D outside = BareBody("M3 Outside Blast", new Vector2(14f, 0f));
        GameObject enemyTemplate = new GameObject("M3 Blast Enemy Template");
        enemyTemplate.SetActive(false);
        RusherAI prototype = enemyTemplate.AddComponent<RusherAI>();
        enemyTemplate.AddComponent<CircleCollider2D>().radius = 0.25f;
        ChildCollider(enemyTemplate, "M3 Enemy Compound Collider");
        SetField(prototype, "baseMoveSpeed", 0f);
        EnemyBase enemy = pool.Get(enemyTemplate, new Vector2(10f, 1.5f), Quaternion.identity).GetComponent<EnemyBase>();
        enemy.Health.ResetHealth(200);
        Physics2D.SyncTransforms();
        barrel.TakeDamage(new DamageInfo { amount = 0 });
        Assert(barrel.HitsRemaining == 2, "Non-positive damage does not advance the barrel hit counter.");
        barrel.TakeDamage(new DamageInfo { amount = 25 });
        Assert(!barrel.IsExploded && barrel.HitsRemaining == 1 && enemy.Health.CurrentHP == 200,
            "The first positive hit only arms the barrel.");
        int explosions = 0;
        Action<Vector2> exploded = position => explosions++;
        GameEvents.BarrelExploded += exploded;
        try
        {
            barrel.TakeDamage(new DamageInfo { amount = 25 });
            barrel.TakeDamage(new DamageInfo { amount = 25 });
            Assert(barrel.IsExploded && explosions == 1 && enemy.Health.CurrentHP == 140,
                "The second hit explodes once and deals sixty damage once per compound target.");
            yield return GameTime(0.03f);
            Assert(Approximately(crate.linearVelocity.magnitude, 6f) && outside.linearVelocity == Vector2.zero,
                "A radius-three blast applies force twelve once to a mass-two body and leaves outside bodies untouched.");
            Assert(barrel.gameObject.activeSelf && !barrel.GetComponent<Collider2D>().enabled,
                "Exploded barrels keep their reset listener active while removing their collision.");
            manager.CompleteWave(manager.CurrentWave);
            yield return Until(() => manager.State == GameState.Shop, 0.5f, "Barrel fixture reaches the between-wave shop.");
            manager.CloseShopAndStartNextWave();
            Assert(!barrel.IsExploded && barrel.HitsRemaining == 2 && barrel.GetComponent<Collider2D>().enabled,
                "A new wave restores the barrel and its hit counter.");

            ExplosiveBarrel chain = Barrel("M3 Chain Barrel", new Vector2(10f, -1.5f));
            chain.TakeDamage(new DamageInfo { amount = 1 });
            Physics2D.SyncTransforms();
            int before = explosions;
            barrel.TakeDamage(new DamageInfo { amount = 1 });
            barrel.TakeDamage(new DamageInfo { amount = 1 });
            Assert(barrel.IsExploded && chain.IsExploded && explosions == before + 2,
                "A blast supplies one hit to an armed neighboring barrel without recursive repeat explosions.");
            manager.ReturnToMainMenu();
            Assert(!barrel.IsExploded && !chain.IsExploded && barrel.HitsRemaining == 2 && chain.HitsRemaining == 2,
                "Returning to the menu restores all scene barrels.");
            Assert(player.Health.CurrentHP == 100, "Distant barrel checks do not damage the player outside their radius.");
            UnityEngine.Object.DestroyImmediate(chain.gameObject);
        }
        finally
        {
            GameEvents.BarrelExploded -= exploded;
        }
        UnityEngine.Object.DestroyImmediate(barrel.gameObject);
        UnityEngine.Object.DestroyImmediate(crate.gameObject);
        UnityEngine.Object.DestroyImmediate(outside.gameObject);

        manager.StartGame();
        ExplosiveBarrel closeBarrel = Barrel("M3 Player Blast", new Vector2(1f, 0f));
        player.TakeDamage(new DamageInfo { amount = 1 });
        Physics2D.SyncTransforms();
        closeBarrel.TakeDamage(new DamageInfo { amount = 1 });
        closeBarrel.TakeDamage(new DamageInfo { amount = 1 });
        yield return GameTime(0.03f);
        Assert(player.Health.CurrentHP == 99 && player.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > 0f,
            "Player invincibility blocks repeated damage but does not cancel a barrel's physical impulse.");
        manager.RestartGame();
        player.Health.ResetHealth(50);
        enemy = pool.Get(enemyTemplate, new Vector2(1f, 1.5f), Quaternion.identity).GetComponent<EnemyBase>();
        enemy.Health.ResetHealth(200);
        Physics2D.SyncTransforms();
        closeBarrel.TakeDamage(new DamageInfo { amount = 1 });
        closeBarrel.TakeDamage(new DamageInfo { amount = 1 });
        Assert(manager.State == GameState.GameOver && enemy.Health.CurrentHP == 140,
            "A lethal player hit does not interrupt damage to the other targets in the same explosion.");
        manager.ReturnToMainMenu();
        UnityEngine.Object.DestroyImmediate(closeBarrel.gameObject);
    }

    private static EnemyProjectile Shot(PoolManager pool, GameObject template, EnemyBase owner, Vector2 direction)
    {
        EnemyProjectile shot = pool.Get(template, owner.transform.position, Quaternion.identity).GetComponent<EnemyProjectile>();
        shot.Launch(direction, owner);
        return shot;
    }

    private static GameObject Solid(string name, Vector2 position, Vector2 size)
    {
        GameObject result = new GameObject(name);
        result.layer = PropLayer;
        result.transform.position = position;
        result.AddComponent<BoxCollider2D>().size = size;
        Physics2D.SyncTransforms();
        return result;
    }

    private static BoxCollider2D ChildCollider(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        BoxCollider2D collider = child.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.25f, 0.25f);
        return collider;
    }

    private static Rigidbody2D BareBody(string name, Vector2 position)
    {
        GameObject result = new GameObject(name);
        result.transform.position = position;
        Rigidbody2D body = result.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.mass = 2f;
        body.linearDamping = 0f;
        body.freezeRotation = true;
        result.AddComponent<BoxCollider2D>().size = new Vector2(0.25f, 0.25f);
        return body;
    }

    private static ExplosiveBarrel Barrel(string name, Vector2 position)
    {
        GameObject result = new GameObject(name);
        result.SetActive(false);
        result.transform.position = position;
        ExplosiveBarrel barrel = result.AddComponent<ExplosiveBarrel>();
        result.SetActive(true);
        return barrel;
    }

    private static IEnumerator Until(Func<bool> condition, float timeout, string message)
    {
        float end = Time.time + timeout;
        while (!condition() && Time.time < end) yield return null;
        Assert(condition(), message);
    }

    private static IEnumerator GameTime(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static IEnumerator Realtime(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target) yield return null;
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

    private static bool Approximately(float actual, float expected)
    {
        return Mathf.Abs(actual - expected) < 0.02f;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
