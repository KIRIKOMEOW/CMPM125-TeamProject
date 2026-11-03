using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GameFeel : MonoBehaviour
{
    [Header("Throw Feedback")]
    [SerializeField] private float throwIntensity = 1f / 32f;
    [SerializeField] private float throwDuration = 0.06f;

    [Header("Hit Feedback")]
    [SerializeField] private float hitIntensity = 2f / 32f;
    [SerializeField] private float hitDuration = 0.08f;
    [SerializeField] private float deathIntensity = 4f / 32f;
    [SerializeField] private float deathDuration = 0.15f;
    [SerializeField] private float deathStopDuration = 0.05f;
    [SerializeField] private float playerHitIntensity = 6f / 32f;
    [SerializeField] private float playerHitDuration = 0.2f;

    [Header("Explosion and Recall")]
    [SerializeField] private float explosionIntensity = 10f / 32f;
    [SerializeField] private float explosionDuration = 0.25f;
    [SerializeField] private float explosionStopDuration = 0.1f;
    [SerializeField] private float recallIntensity = 2f / 32f;
    [SerializeField] private float recallDuration = 0.12f;

    private static GameFeel activeInstance;
    private static bool feelEnabled = true;
    public static event Action<bool> EnabledChanged;

    public static bool Enabled
    {
        get => feelEnabled;
        set
        {
            if (feelEnabled == value) return;
            feelEnabled = value;
            EnabledChanged?.Invoke(value);
        }
    }

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Destroy(this);
            return;
        }
        activeInstance = this;
    }

    private void OnEnable()
    {
        if (activeInstance != this) return;
        GameEvents.BladeThrown += OnBladeThrown;
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.BarrelExploded += OnBarrelExploded;
        GameEvents.RecallStarted += OnRecallStarted;
    }

    private void OnDisable()
    {
        GameEvents.BladeThrown -= OnBladeThrown;
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.BarrelExploded -= OnBarrelExploded;
        GameEvents.RecallStarted -= OnRecallStarted;
    }

    private void OnDestroy()
    {
        if (activeInstance == this) activeInstance = null;
    }

    private void Update()
    {
        if (activeInstance == this && Input.GetKeyDown(KeyCode.F1)) Enabled = !Enabled;
    }

    private void OnBladeThrown(Vector2 position) => Feedback(throwIntensity, throwDuration);
    private void OnEnemyHit(Vector2 position, bool fromRecall) => Feedback(hitIntensity, hitDuration);
    private void OnEnemyKilled(Vector2 position, EnemyType type) => Feedback(deathIntensity, deathDuration, deathStopDuration);
    private void OnPlayerHit(Vector2 position) => Feedback(playerHitIntensity, playerHitDuration);
    private void OnBarrelExploded(Vector2 position) => Feedback(explosionIntensity, explosionDuration, explosionStopDuration);
    private void OnRecallStarted() => Feedback(recallIntensity, recallDuration);

    private void Feedback(float intensity, float duration, float stopDuration = 0f)
    {
        if (!Enabled) return;
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(intensity, duration);
        if (stopDuration > 0f && HitStop.Instance != null) HitStop.Instance.Stop(stopDuration);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeInstance = null;
        feelEnabled = true;
        EnabledChanged = null;
    }
}
