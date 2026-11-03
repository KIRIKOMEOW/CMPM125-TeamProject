using UnityEngine;

[DisallowMultipleComponent]
public class BruteAI : EnemyBase
{
    private enum BruteState { Pursue, WindUp, Charge, Recover }

    [Header("Brute Stats")]
    [SerializeField] private int bruteMaxHP = 200;
    [SerializeField] private float bruteMoveSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0.5f;

    [Header("Charge")]
    [SerializeField] private float chargeTriggerRange = 7f;
    [SerializeField] private float windupDuration = 0.8f;
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeDuration = 0.5f;
    [SerializeField] private float recoveryDuration = 0.5f;
    [SerializeField] private float chargeCooldown = 2.5f;
    [SerializeField] private LayerMask chargeBlockingLayers;

    [Header("Contact Attack")]
    [SerializeField] private int contactDamage = 20;
    [SerializeField] private float contactKnockback = 6f;

    private BruteState state;
    private Vector2 chargeDirection;
    private float phaseUntil;
    private float nextChargeAt;

    public override EnemyType Type => EnemyType.Brute;
    protected override int SpawnMaxHP => bruteMaxHP;
    protected override float BaseMoveSpeed => bruteMoveSpeed;
    protected override float KnockbackScale => base.KnockbackScale * knockbackResistance;
    public string CurrentStateName => state.ToString();
    public bool IsCharging => state == BruteState.Charge;

    protected override void Awake()
    {
        base.Awake();
        Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        state = BruteState.Pursue;
        chargeDirection = Vector2.right;
        phaseUntil = 0f;
        nextChargeAt = Time.time;
    }

    public override void OnDespawn()
    {
        state = BruteState.Pursue;
        chargeDirection = Vector2.right;
        phaseUntil = 0f;
        nextChargeAt = 0f;
        base.OnDespawn();
    }

    protected override void FixedUpdate()
    {
        if (IsRooted && (state == BruteState.WindUp || state == BruteState.Charge))
        {
            // Root cancels the attack even while the base class is preserving an external impulse.
            state = BruteState.Recover;
            phaseUntil = Time.time + recoveryDuration;
            nextChargeAt = Time.time + chargeCooldown;
        }
        base.FixedUpdate();
    }

    protected override void TickMovement()
    {
        if (Target == null || Target.Health == null || Target.Health.IsDead)
        {
            if (state == BruteState.WindUp || state == BruteState.Charge)
                FinishCharge();
            StopMotion();
            return;
        }

        switch (state)
        {
            case BruteState.Pursue:
                Vector2 toPlayer = (Vector2)Target.transform.position - Body.position;
                if (Time.time >= nextChargeAt && toPlayer.sqrMagnitude <= chargeTriggerRange * chargeTriggerRange)
                {
                    chargeDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
                    state = BruteState.WindUp;
                    phaseUntil = Time.time + windupDuration;
                    StopMotion();
                }
                else
                {
                    Move(toPlayer);
                }
                break;
            case BruteState.WindUp:
                StopMotion();
                if (Time.time >= phaseUntil)
                {
                    state = BruteState.Charge;
                    phaseUntil = Time.time + chargeDuration;
                    MoveCharge();
                }
                break;
            case BruteState.Charge:
                if (Time.time >= phaseUntil)
                    FinishCharge();
                else
                    MoveCharge();
                break;
            case BruteState.Recover:
                StopMotion();
                if (Time.time >= phaseUntil)
                    state = BruteState.Pursue;
                break;
        }
    }

    private void MoveCharge()
    {
        float waveMultiplier = bruteMoveSpeed > 0.0001f ? MoveSpeed / bruteMoveSpeed : 1f;
        Body.linearVelocity = chargeDirection * (chargeSpeed * waveMultiplier);
    }

    private void FinishCharge()
    {
        state = BruteState.Recover;
        phaseUntil = Time.time + recoveryDuration;
        nextChargeAt = Time.time + chargeCooldown;
        StopMotion();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void HandleContact(Collider2D other)
    {
        if (!CanAct || other == null)
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null && other.attachedRigidbody != null)
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        if (player != null)
        {
            IDamageable damageable = player.GetComponent<IDamageable>();
            Vector2 away = (Vector2)player.transform.position - Body.position;
            damageable?.TakeDamage(new DamageInfo
            {
                amount = contactDamage,
                knockback = (away.sqrMagnitude > 0.0001f ? away.normalized : chargeDirection) * contactKnockback,
                fromRecall = false,
                source = gameObject
            });
        }

        if (state == BruteState.Charge && (chargeBlockingLayers.value & (1 << other.gameObject.layer)) != 0)
            FinishCharge();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = state == BruteState.WindUp ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(transform.position, chargeTriggerRange);
        if (Application.isPlaying && (state == BruteState.WindUp || state == BruteState.Charge))
            Gizmos.DrawRay(transform.position, chargeDirection * chargeSpeed * chargeDuration);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        bruteMaxHP = Mathf.Max(1, bruteMaxHP);
        bruteMoveSpeed = Mathf.Max(0f, bruteMoveSpeed);
        knockbackResistance = Mathf.Clamp01(knockbackResistance);
        chargeTriggerRange = Mathf.Max(0f, chargeTriggerRange);
        windupDuration = Mathf.Max(0f, windupDuration);
        chargeSpeed = Mathf.Max(0f, chargeSpeed);
        chargeDuration = Mathf.Max(0.01f, chargeDuration);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
        chargeCooldown = Mathf.Max(0f, chargeCooldown);
        contactDamage = Mathf.Max(1, contactDamage);
        contactKnockback = Mathf.Max(0f, contactKnockback);
    }
}
