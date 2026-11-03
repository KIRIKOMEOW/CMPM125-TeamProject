using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BladeRecall : MonoBehaviour
{
    [Header("Recall")]
    [SerializeField] private float cooldown = 4f;
    [SerializeField] private int rootHitCount = 3;
    [SerializeField] private float rootDuration = 1f;

    private PlayerController player;
    private Coroutine recallRoutine;

    public float CooldownRemaining { get; private set; }
    public float Cooldown => cooldown;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void OnDisable()
    {
        ResetRecall();
    }

    public bool TryRecall()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing
            || player == null || player.Health.IsDead || CooldownRemaining > 0f) return false;
        List<Blade> blades = new List<Blade>();
        player.CopyRecallableBlades(blades);
        if (blades.Count == 0) return false;
        recallRoutine = StartCoroutine(RecallSequence(blades));
        return true;
    }

    private IEnumerator RecallSequence(List<Blade> blades)
    {
        // Each volley owns its hit counts even if an older, long-distance return is still in flight.
        RecallVolley volley = new RecallVolley(rootHitCount, rootDuration);
        CooldownRemaining = cooldown;
        foreach (Blade blade in blades)
            if (blade != null) blade.BeginRecall(volley);
        GameEvents.RaiseRecallStarted();
        while (CooldownRemaining > 0f)
        {
            PublishStatus();
            yield return null;
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
        }
        PublishStatus();
        recallRoutine = null;
    }

    public void ResetRecall()
    {
        if (recallRoutine != null) StopCoroutine(recallRoutine);
        recallRoutine = null;
        CooldownRemaining = 0f;
        PublishStatus();
    }

    public void PublishStatus()
    {
        GameEvents.RaiseRecallCooldownChanged(CooldownRemaining, cooldown);
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0.01f, cooldown);
        rootHitCount = Mathf.Max(1, rootHitCount);
        rootDuration = Mathf.Max(0f, rootDuration);
    }

    internal sealed class RecallVolley
    {
        private struct HitCount
        {
            public int spawnId;
            public int count;
        }

        private readonly Dictionary<EnemyBase, HitCount> enemyHits = new Dictionary<EnemyBase, HitCount>();
        private readonly int hitsRequired;
        private readonly float duration;

        internal RecallVolley(int hitsRequired, float duration)
        {
            this.hitsRequired = Mathf.Max(1, hitsRequired);
            this.duration = duration;
        }

        internal void RegisterHit(EnemyBase enemy, int spawnId)
        {
            if (!enemy.isActiveAndEnabled || enemy.SpawnId != spawnId || enemy.Health.IsDead) return;
            enemyHits.TryGetValue(enemy, out HitCount entry);
            if (entry.spawnId != spawnId) entry = new HitCount { spawnId = spawnId };
            entry.count++;
            enemyHits[enemy] = entry;
            if (entry.count == hitsRequired) enemy.ApplyRoot(duration);
        }
    }
}
