using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M5Verification
{
    private const string PendingKey = "NightShift.M5Verification.Pending";
    private static IEnumerator checks;
    private static double beginAfter;
    private static double deadline;
    private static string runtimeError;

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying)
            throw new InvalidOperationException("Run M5 verification in a batch editor starting from Edit Mode.");
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
        if (!SessionState.GetBool(PendingKey, false) || checks != null) return;
        SessionState.SetBool(PendingKey, false);
        runtimeError = null;
        Debug.Log("M5_VERIFY START: editor startup finished; creating runtime-only test objects.");
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 30d;
        checks = Verify();
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message;
    }

    private static void Tick()
    {
        try
        {
            Assert(EditorApplication.timeSinceStartup < deadline, "Verification timed out.");
            Assert(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        (checks as IDisposable)?.Dispose();
        checks = null;
        if (exception == null) Debug.Log("M5_VERIFY PASS: fan unlock/ammo/cooldown/input/recall, Brute health/speed/knockback/windup/pause/root/cover/pool reset.");
        else Debug.LogError("M5_VERIFY FAIL: " + exception);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static IEnumerator Verify()
    {
        if (AudioManager.Instance != null) UnityEngine.Object.DestroyImmediate(AudioManager.Instance.gameObject);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);
        PoolManager pool = new GameObject("M5 Pool").AddComponent<PoolManager>();
        GameObject bladeTemplate = new GameObject("M5 Blade Template");
        bladeTemplate.SetActive(false);
        bladeTemplate.AddComponent<Blade>();
        bladeTemplate.GetComponent<CircleCollider2D>().radius = 0.1f;
        GameObject playerObject = new GameObject("M5 Player");
        playerObject.SetActive(false);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        playerObject.AddComponent<CircleCollider2D>().radius = 0.3f;
        SetField(player, "bladePrefab", bladeTemplate);
        playerObject.SetActive(true);
        GameManager manager = new GameObject("M5 Manager").AddComponent<GameManager>();
        int frame = Time.frameCount;
        while (Time.frameCount == frame) yield return null;
        manager.StartGame();
        Assert(player.FanThrowAvailable && !player.FanThrowUnlocked && !player.TryFanThrow() && player.BladesInHand == 5,
            "The available fan attack remains locked until its upgrade is applied.");
        player.ApplyUpgrades(0, 0, 0, 0, true);

        GameObject targetTemplate = new GameObject("M5 Recall Target Template");
        targetTemplate.SetActive(false);
        RusherAI targetPrototype = targetTemplate.AddComponent<RusherAI>();
        targetTemplate.AddComponent<CircleCollider2D>().radius = 2f;
        SetField(targetPrototype, "baseMoveSpeed", 0f);
        SetField(targetPrototype, "knockbackMultiplier", 0f);
        EnemyBase target = pool.Get(targetTemplate, new Vector2(4f, 0f), Quaternion.identity).GetComponent<EnemyBase>();
        target.Health.ResetHealth(300);
        int thrown = 0;
        Action<Vector2> onThrow = position => thrown++;
        GameEvents.BladeThrown += onThrow;
        try
        {
            Assert(player.TryFanThrow() && player.BladesInHand == 2 && thrown == 3,
                "An unlocked fan reserves and launches three blades, consuming exactly three ammo.");
            Blade[] blades = UnityEngine.Object.FindObjectsByType<Blade>();
            float minimumAngle = float.PositiveInfinity;
            float maximumAngle = float.NegativeInfinity;
            foreach (Blade blade in blades)
            {
                float angle = Mathf.DeltaAngle(0f, blade.GetComponent<Rigidbody2D>().rotation);
                minimumAngle = Mathf.Min(minimumAngle, angle);
                maximumAngle = Mathf.Max(maximumAngle, angle);
            }
            Assert(blades.Length == 3 && Approximately(minimumAngle, -15f) && Approximately(maximumAngle, 15f),
                "The three blades span a symmetric thirty-degree fan.");
            Assert(!player.TryThrow() && !player.TryFanThrow() && player.BladesInHand == 2,
                "Fan and single throws share their attack cooldown.");
            float until = Time.time + 0.75f;
            while (Time.time < until) yield return null;
            Assert(!player.TryFanThrow() && player.BladesInHand == 2 && thrown == 3,
                "Insufficient ammo cannot launch a partial fan after the cooldown ends.");
            Assert(target.Health.CurrentHP == 225, "Every outgoing fan blade damages a shared target once.");
            BladeRecall recall = player.GetComponent<BladeRecall>();
            Assert(recall.TryRecall(), "All fan blades can be recalled together.");
            until = Time.time + 1f;
            while (!target.IsRooted && Time.time < until) yield return null;
            Assert(target.IsRooted && target.Health.CurrentHP == 150,
                "Three returning fan blades each hit once and share the recall root threshold.");
            until = Time.time + 1f;
            while (player.BladesInHand < 5 && Time.time < until) yield return null;
            Assert(player.BladesInHand == 5, "Returning a fan restores all three inventory slots.");
        }
        finally { GameEvents.BladeThrown -= onThrow; }

        manager.RestartGame();
        Assert(!player.FanThrowUnlocked && player.BladesInHand == 5, "Restart removes the purchased fan ability and restores ammo.");
        player.ApplyUpgrades(0, 0, 0, 0, true);
        SetField(player, "bladePrefab", null);
        Assert(!player.TryFanThrow() && player.BladesInHand == 5, "A missing blade prefab cannot consume fan ammo.");
        GameObject invalidTemplate = new GameObject("M5 Invalid Blade Template");
        invalidTemplate.SetActive(false);
        SetField(player, "bladePrefab", invalidTemplate);
        Assert(!player.TryFanThrow() && player.BladesInHand == 5 && UnityEngine.Object.FindObjectsByType<Blade>().Length == 0,
            "A prefab without a Blade component cannot leave a partial attack or charge ammo.");
        SetField(player, "bladePrefab", bladeTemplate);
        Assert(player.TryThrow() && !player.TryFanThrow() && player.BladesInHand == 4,
            "Failed setup does not spend cooldown, while a successful single throw blocks an immediate fan.");
        manager.RestartGame();
        player.ApplyUpgrades(0, 0, 0, 0, true);
        manager.PauseGame();
        manager.ResumeGame();
        Call(player, "HandleFanThrowInput", true);
        Call(player, "HandleFanThrowInput", true);
        Assert(player.BladesInHand == 5, "A held right mouse button cannot become an attack when a menu resumes.");
        Call(player, "HandleFanThrowInput", false);
        Call(player, "HandleFanThrowInput", true);
        Assert(player.BladesInHand == 2, "A fresh right-button press can throw after release.");
        manager.RestartGame();

        GameObject bruteTemplate = new GameObject("M5 Brute Template");
        bruteTemplate.SetActive(false);
        BruteAI prototype = bruteTemplate.AddComponent<BruteAI>();
        bruteTemplate.AddComponent<CircleCollider2D>().radius = 0.5f;
        SetField(prototype, "chargeBlockingLayers", (LayerMask)(1 << 31));
        BruteAI brute = pool.Get(bruteTemplate, new Vector2(20f, 0f), Quaternion.identity).GetComponent<BruteAI>();
        Assert(brute.Type == EnemyType.Brute && brute.Health.CurrentHP == 200 && Approximately(brute.MoveSpeed, 2f),
            "Brute defaults are two hundred HP and speed two.");
        brute.Initialize(6, player);
        Assert(Approximately(brute.MoveSpeed, 2.5f), "Brute inherits the five-percent-per-wave speed curve.");
        brute.Initialize(20, player);
        Assert(Approximately(brute.MoveSpeed, 2.8f), "Brute speed bonuses stop at forty percent.");
        Rigidbody2D bruteBody = brute.GetComponent<Rigidbody2D>();
        bruteBody.linearDamping = 0f;
        brute.TakeDamage(new DamageInfo { amount = 1, knockback = Vector2.right * 4f });
        float impulseUntil = Time.time + 0.03f;
        while (Time.time < impulseUntil) yield return null;
        Assert(brute.Health.CurrentHP == 199 && Approximately(bruteBody.linearVelocity.x, 2f),
            "A mass-one Brute takes normal damage but half the four-unit knockback impulse.");
        pool.Return(brute.gameObject);
        brute = pool.Get(bruteTemplate, new Vector2(5f, 0f), Quaternion.identity).GetComponent<BruteAI>();
        bruteBody = brute.GetComponent<Rigidbody2D>();
        float stateUntil = Time.time + 0.5f;
        while (brute.CurrentStateName != "WindUp" && Time.time < stateUntil) yield return null;
        Assert(brute.CurrentStateName == "WindUp" && bruteBody.linearVelocity == Vector2.zero,
            "A nearby player triggers a stationary charge wind-up.");
        manager.PauseGame();
        double realUntil = EditorApplication.timeSinceStartup + 0.15d;
        while (EditorApplication.timeSinceStartup < realUntil) yield return null;
        Assert(brute.CurrentStateName == "WindUp", "Pausing freezes the wind-up timer.");
        manager.ResumeGame();
        player.GetComponent<Rigidbody2D>().position = new Vector2(0f, 3f);
        Physics2D.SyncTransforms();
        stateUntil = Time.time + 0.45f;
        while (Time.time < stateUntil) yield return null;
        Assert(!brute.IsCharging, "A charge cannot start before its eight-tenths-second wind-up.");
        stateUntil = Time.time + 0.5f;
        while (!brute.IsCharging && Time.time < stateUntil) yield return null;
        Assert(brute.IsCharging && bruteBody.linearVelocity.x < 0f && Mathf.Abs(bruteBody.linearVelocity.y) < 0.01f,
            "The completed charge follows the direction locked at wind-up, rather than homing on the moved player.");
        brute.ApplyRoot(0.2f);
        stateUntil = Time.time + 0.03f;
        while (Time.time < stateUntil) yield return null;
        Assert(!brute.IsCharging && brute.CurrentStateName == "Recover" && bruteBody.linearVelocity == Vector2.zero,
            "Rooting interrupts an active charge and clears its movement.");
        pool.Return(brute.gameObject);
        brute = pool.Get(bruteTemplate, new Vector2(20f, 0f), Quaternion.identity).GetComponent<BruteAI>();
        Assert(brute.CurrentStateName == "Pursue" && !brute.IsRooted && brute.Health.CurrentHP == 200,
            "Pool reuse clears charge/root state and restores full Brute health.");
        manager.RestartGame();

        GameObject wall = new GameObject("M5 Charge Cover");
        wall.layer = 31;
        wall.transform.position = new Vector2(3f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 4f);
        brute = pool.Get(bruteTemplate, new Vector2(5f, 0f), Quaternion.identity).GetComponent<BruteAI>();
        Physics2D.SyncTransforms();
        stateUntil = Time.time + 1.6f;
        while (brute.CurrentStateName != "Recover" && Time.time < stateUntil) yield return null;
        Assert(brute.CurrentStateName == "Recover" && brute.transform.position.x >= 3.55f && player.Health.CurrentHP == 100,
            "Solid cover stops a fast Brute charge before it can tunnel through or reach the player.");
        manager.ReturnToMainMenu();
        Assert(UnityEngine.Object.FindObjectsByType<BruteAI>().Length == 0 && player.BladesInHand == 5 && !player.FanThrowUnlocked,
            "Returning to the menu retires Brutes and resets the fan inventory and unlock.");
    }

    private static void SetField(object target, string name, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void Call(object target, string name, object value) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new[] { value });
    private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) < 0.02f;
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
