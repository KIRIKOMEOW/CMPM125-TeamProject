using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M4Verification
{
    private const string PendingKey = "NightShift.M4Verification.Pending";
    private const string VolumeKey = "NightShift.MasterVolume";
    private static IEnumerator checks;
    private static double beginAfter;
    private static double deadline;
    private static string runtimeError;

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying)
            throw new InvalidOperationException("Run M4 verification in a batch editor starting from Edit Mode.");
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
        Debug.Log("M4_VERIFY START: editor startup finished; creating runtime-only test objects.");
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
        if (exception == null) Debug.Log("M4_VERIFY PASS: realtime freeze/state ownership, pixel shake, feel/flash cancellation, null-safe audio/volume and pickup-only coin events.");
        else Debug.LogError("M4_VERIFY FAIL: " + exception);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static IEnumerator Verify()
    {
        bool hadVolume = PlayerPrefs.HasKey(VolumeKey);
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        float listenerVolume = AudioListener.volume;
        bool savedFeel = GameFeel.Enabled;
        Material normal = null;
        Material white = null;
        try
        {
            if (AudioManager.Instance != null) UnityEngine.Object.DestroyImmediate(AudioManager.Instance.gameObject);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    UnityEngine.Object.DestroyImmediate(root);
            PoolManager pool = new GameObject("M4 Pool").AddComponent<PoolManager>();
            GameObject playerObject = new GameObject("M4 Player");
            playerObject.SetActive(false);
            PlayerController player = playerObject.AddComponent<PlayerController>();
            playerObject.SetActive(true);
            GameManager manager = new GameObject("M4 Manager").AddComponent<GameManager>();
            HitStop hitStop = new GameObject("M4 Hit Stop").AddComponent<HitStop>();
            new GameObject("M4 Game Feel").AddComponent<GameFeel>();
            GameObject rig = new GameObject("M4 Camera Rig");
            GameObject cameraObject = new GameObject("M4 Shake Target");
            cameraObject.transform.SetParent(rig.transform, false);
            Vector3 basePosition = new Vector3(0.2f, 0.4f, -10f);
            cameraObject.transform.localPosition = basePosition;
            CameraShake shake = cameraObject.AddComponent<CameraShake>();
            int frame = Time.frameCount;
            while (Time.frameCount == frame) yield return null;
            manager.StartGame();
            GameFeel.Enabled = true;

            hitStop.Stop(0.1f);
            Assert(Time.timeScale == 0f && manager.State == GameState.Playing, "Hit stop temporarily freezes an active game.");
            double until = EditorApplication.timeSinceStartup + 0.04d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            hitStop.Stop(0.2f);
            until = EditorApplication.timeSinceStartup + 0.1d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(Time.timeScale == 0f, "An older hit-stop deadline cannot end a later overlapping stop.");
            until = EditorApplication.timeSinceStartup + 0.2d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(Time.timeScale == 1f, "Hit stop restores gameplay using realtime while scaled time is frozen.");

            hitStop.Stop(0.05f);
            manager.PauseGame();
            until = EditorApplication.timeSinceStartup + 0.1d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(manager.State == GameState.Paused && Time.timeScale == 0f, "A pending hit stop cannot undo Pause.");
            GameFeel.Enabled = false;
            Assert(Time.timeScale == 0f, "Disabling feedback must preserve a menu-owned pause.");
            manager.ResumeGame();
            GameFeel.Enabled = true;
            hitStop.Stop(0.05f);
            Call(manager, "SetState", GameState.Shop);
            until = EditorApplication.timeSinceStartup + 0.1d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            hitStop.Stop(0.1f);
            Assert(manager.State == GameState.Shop && Time.timeScale == 0f, "Shop owns its pause even after a previous hit-stop deadline.");
            manager.CloseShopAndStartNextWave();

            manager.PauseGame();
            shake.Shake(0.5f, 2f);
            frame = Time.frameCount + 3;
            while (Time.frameCount < frame) yield return null;
            Vector3 offset = cameraObject.transform.localPosition - basePosition;
            Assert(OnPixel(offset.x) && OnPixel(offset.y) && Mathf.Abs(offset.z) < 0.0001f,
                "Shake displacement stays on a one-thirty-second-unit grid, even while paused.");
            rig.transform.position = new Vector3(5f, 7f, 0f);
            GameFeel.Enabled = false;
            Assert(Vector3.Distance(cameraObject.transform.localPosition, basePosition) < 0.0001f
                && rig.transform.position == new Vector3(5f, 7f, 0f), "Feedback-off removes only its own offset and preserves rig motion.");
            GameFeel.Enabled = true;
            shake.Shake(0.25f, 0.05f);
            until = EditorApplication.timeSinceStartup + 0.15d;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(Vector3.Distance(cameraObject.transform.localPosition, basePosition) < 0.0001f,
                "Shake expires and restores its base position using unscaled time.");
            manager.ResumeGame();

            Shader shader = Shader.Find("Sprites/Default");
            Assert(shader != null, "The built-in sprite shader is available for temporary feedback materials.");
            normal = new Material(shader);
            white = new Material(shader);
            GameObject healthObject = new GameObject("M4 Flash Fixture");
            healthObject.SetActive(false);
            SpriteRenderer sprite = healthObject.AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = normal;
            Health health = healthObject.AddComponent<Health>();
            SetField(health, "flashMaterial", white);
            healthObject.SetActive(true);
            hitStop.Stop(0.2f);
            health.ApplyDamage(new DamageInfo { amount = 1 });
            Assert(sprite.sharedMaterial == white, "An enabled hit flash swaps the temporary material.");
            GameFeel.Enabled = false;
            Assert(Time.timeScale == 1f && sprite.sharedMaterial == normal,
                "Feedback-off immediately cancels its freeze and restores an in-progress hit flash.");
            health.ApplyDamage(new DamageInfo { amount = 1 });
            GameEvents.RaiseEnemyKilled(Vector2.zero, EnemyType.Rusher);
            Assert(health.CurrentHP == 98 && sprite.sharedMaterial == normal && Time.timeScale == 1f,
                "Feedback-off skips visual freezes and flashes while gameplay damage still works.");
            GameFeel.Enabled = true;
            GameEvents.RaiseEnemyKilled(Vector2.zero, EnemyType.Rusher);
            Assert(Time.timeScale == 0f, "Enemy death feedback is connected to hit stop when enabled.");
            manager.RestartGame();
            Assert(Time.timeScale == 1f && Vector3.Distance(cameraObject.transform.localPosition, basePosition) < 0.0001f,
                "Run reset clears feedback before starting the new wave.");

            AudioManager audio = new GameObject("M4 Audio").AddComponent<AudioManager>();
            foreach (SfxId id in Enum.GetValues(typeof(SfxId))) audio.PlaySfx(id);
            audio.PlaySfx((SfxId)99);
            audio.PlayMusic();
            audio.StopMusic();
            audio.MasterVolume = 2f;
            Assert(audio.MasterVolume == 1f && AudioListener.volume == 1f && PlayerPrefs.GetFloat(VolumeKey) == 1f,
                "Master volume clamps high input and updates the listener and preference.");
            audio.MasterVolume = -1f;
            Assert(audio.MasterVolume == 0f && AudioListener.volume == 0f, "Master volume clamps low input.");
            audio.MasterVolume = 0.4f;
            Assert(Mathf.Abs(PlayerPrefs.GetFloat(VolumeKey) - 0.4f) < 0.0001f, "Master volume persists its requested value.");

            int collections = 0;
            int collectedAmount = 0;
            Action<Vector2, int> onCoin = (position, amount) => { collections++; collectedAmount += amount; };
            GameEvents.CoinCollected += onCoin;
            try
            {
                manager.AddCoins(100);
                manager.TrySpendCoins(10);
                Assert(collections == 0, "Currency initialization or spending is not a pickup sound event.");
                GameObject template = new GameObject("M4 Coin Template");
                template.SetActive(false);
                template.AddComponent<Pickup>();
                Pickup coin = pool.Get(template, new Vector2(100f, 0f), Quaternion.identity).GetComponent<Pickup>();
                coin.Initialize(PickupType.Coin, 3, player);
                Assert(coin.TryCollect(player) && !coin.TryCollect(player) && collections == 1 && collectedAmount == 3,
                    "A collected pooled coin raises exactly one semantic collection event.");
                DropTable drops = new GameObject("M4 Drop Table").AddComponent<DropTable>();
                SetField(drops, "extraPickupChance", 0f);
                drops.Drop(Vector2.zero, EnemyType.Rusher);
                Assert(collections == 2 && collectedAmount == 6, "Missing-art fallback coins also emit a collection event.");
                coin = pool.Get(template, new Vector2(100f, 0f), Quaternion.identity).GetComponent<Pickup>();
                coin.Initialize(PickupType.Coin, 5, player);
                Call(manager, "SetState", GameState.Shop);
                manager.TrySpendCoins(1);
                Assert(collections == 3 && collectedAmount == 11, "Shop settlement emits one collection event, while purchases do not.");
                manager.ReturnToMainMenu();
                Assert(collections == 3, "Run reset does not fabricate a coin collection.");
            }
            finally { GameEvents.CoinCollected -= onCoin; }
        }
        finally
        {
            GameFeel.Enabled = savedFeel;
            if (normal != null) UnityEngine.Object.DestroyImmediate(normal);
            if (white != null) UnityEngine.Object.DestroyImmediate(white);
            if (hadVolume) PlayerPrefs.SetFloat(VolumeKey, savedVolume);
            else PlayerPrefs.DeleteKey(VolumeKey);
            AudioListener.volume = listenerVolume;
        }
    }

    private static bool OnPixel(float value) => Mathf.Abs(value * 32f - Mathf.Round(value * 32f)) < 0.0001f;
    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name, object value) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new[] { value });
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
