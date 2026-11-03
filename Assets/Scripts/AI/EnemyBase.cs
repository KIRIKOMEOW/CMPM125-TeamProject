using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public abstract class EnemyBase : MonoBehaviour, IDamageable, IPoolable
{
    public static event Action<EnemyBase, int> Died;

    [Header("Enemy Stats")]
    [SerializeField] private EnemyType enemyType = EnemyType.Rusher;
    [SerializeField] private int maxHP = 50;
    [SerializeField] private float baseMoveSpeed = 3.5f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float knockbackMultiplier = 1f;
    [SerializeField] private float knockbackRecoveryTime = 0.15f;

    [Header("References")]
    [SerializeField] private PlayerController target;

    private Rigidbody2D body;
    private Health health;
    private bool spawned;
    private bool deathHandled;
    private float rootUntil;
    private float knockbackUntil;

    public virtual EnemyType Type => enemyType;
    public Health Health => health;
    public int SpawnId { get; private set; }
    public float MoveSpeed { get; private set; }
    public bool IsRooted => spawned && Time.time < rootUntil;

    protected Rigidbody2D Body => body;
    protected PlayerController Target => target;
    protected virtual int SpawnMaxHP => maxHP;
    protected virtual float KnockbackScale => knockbackMultiplier;
    protected bool CanAct => spawned && !deathHandled && health != null && !health.IsDead
        && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    protected virtual void OnEnable()
    {
        health.Damaged += HandleDamage;
        health.Died += HandleDeath;
    }

    protected virtual void OnDisable()
    {
        health.Damaged -= HandleDamage;
        health.Died -= HandleDeath;
        StopMotion();
    }

    protected virtual void Start()
    {
        // Scene-placed enemies use the same initialization as pooled enemies.
        if (!spawned)
            OnSpawn();
    }

    public virtual void OnSpawn()
    {
        spawned = true;
        deathHandled = false;
        SpawnId++;
        rootUntil = 0f;
        knockbackUntil = 0f;
        StopMotion();
        health.ResetHealth(SpawnMaxHP);

        GameManager manager = GameManager.Instance;
        PlayerController player = manager != null ? manager.Player : target;
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        Initialize(manager != null ? manager.CurrentWave : 1, player);
    }

    public void Initialize(int wave, PlayerController player)
    {
        target = player;
        float bonus = Mathf.Min(0.4f, Mathf.Max(0, wave - 1) * 0.05f);
        MoveSpeed = Mathf.Max(0f, baseMoveSpeed) * (1f + bonus);
    }

    public virtual void OnDespawn()
    {
        spawned = false;
        rootUntil = 0f;
        knockbackUntil = 0f;
        StopAllCoroutines();
        StopMotion();
    }

    public void Despawn()
    {
        if (!gameObject.activeSelf)
            return;

        if (PoolManager.Instance != null)
            PoolManager.Instance.Return(gameObject);
        else
        {
            OnDespawn();
            gameObject.SetActive(false);
        }
    }

    public void TakeDamage(DamageInfo info)
    {
        if (CanAct)
            health.ApplyDamage(info);
    }

    public void ApplyRoot(float duration)
    {
        if (!spawned || deathHandled || health.IsDead || duration <= 0f)
            return;

        rootUntil = Mathf.Max(rootUntil, Time.time + duration);
        StopMotion();
    }

    protected virtual void FixedUpdate()
    {
        if (!CanAct)
        {
            StopMotion();
            return;
        }

        // Let the physics impulse move the enemy before pursuit takes control again.
        if (Time.time < knockbackUntil)
            return;

        if (IsRooted)
        {
            StopMotion();
            return;
        }

        TickMovement();
    }

    protected abstract void TickMovement();

    protected void Move(Vector2 direction)
    {
        Vector2 desiredVelocity = direction.sqrMagnitude > 0.0001f
            ? direction.normalized * MoveSpeed : Vector2.zero;
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }

    protected void StopMotion()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void HandleDamage(DamageInfo info)
    {
        if (!IsRooted)
            ApplyKnockback(info.knockback);
        GameEvents.RaiseEnemyHit(transform.position, info.fromRecall);
    }

    public void ApplyKnockback(Vector2 impulse)
    {
        if (body == null || impulse.sqrMagnitude <= 0f)
            return;

        body.AddForce(impulse * KnockbackScale, ForceMode2D.Impulse);
        knockbackUntil = Time.time + knockbackRecoveryTime;
    }

    private void HandleDeath()
    {
        if (deathHandled)
            return;

        deathHandled = true;
        int dyingSpawnId = SpawnId;
        if (DropTable.Instance != null)
            DropTable.Instance.Drop(transform.position, Type);
        if (SpawnId != dyingSpawnId || !spawned)
            return;

        Died?.Invoke(this, dyingSpawnId);
        if (SpawnId != dyingSpawnId || !spawned)
            return;

        GameEvents.RaiseEnemyKilled(transform.position, Type);
        if (SpawnId == dyingSpawnId && spawned)
            Despawn();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubscribers()
    {
        Died = null;
    }

    protected virtual void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
        knockbackRecoveryTime = Mathf.Max(0f, knockbackRecoveryTime);
    }
}
