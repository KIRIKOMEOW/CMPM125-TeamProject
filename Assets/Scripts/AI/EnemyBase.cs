using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public abstract class EnemyBase : MonoBehaviour, IDamageable, IPoolable
{
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

    public EnemyType Type => enemyType;
    public Health Health => health;
    public int SpawnId { get; private set; }
    public float MoveSpeed { get; private set; }
    public bool IsRooted => spawned && Time.time < rootUntil;

    protected Rigidbody2D Body => body;
    protected PlayerController Target => target;
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
        health.ResetHealth(maxHP);

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
        if (!CanAct || IsRooted)
        {
            StopMotion();
            return;
        }

        // Let the physics impulse move the enemy before pursuit takes control again.
        if (Time.time < knockbackUntil)
            return;

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
        if (info.knockback.sqrMagnitude > 0f && !IsRooted)
        {
            body.AddForce(info.knockback * knockbackMultiplier, ForceMode2D.Impulse);
            knockbackUntil = Time.time + knockbackRecoveryTime;
        }
        GameEvents.RaiseEnemyHit(transform.position, info.fromRecall);
    }

    private void HandleDeath()
    {
        if (deathHandled)
            return;

        deathHandled = true;
        int dyingSpawnId = SpawnId;
        GameEvents.RaiseEnemyKilled(transform.position, enemyType);
        if (SpawnId == dyingSpawnId && spawned)
            Despawn();
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
