using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class EnemyProjectile : MonoBehaviour, IPoolable
{
    [Header("Projectile")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float knockback = 3f;
    [SerializeField] private float maxRange = 16f;
    [SerializeField] private float lifetime = 4f;

    [Header("Collision")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField] private LayerMask hitLayers = ~0;

    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private Rigidbody2D body;
    private CircleCollider2D hitCollider;
    private EnemyBase owner;
    private Vector2 direction;
    private float distanceTravelled;
    private float remainingLifetime;
    private bool launched;
    private int spawnId;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<CircleCollider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        hitCollider.isTrigger = true;
    }

    public void OnSpawn()
    {
        spawnId++;
        launched = false;
        owner = null;
        distanceTravelled = 0f;
        remainingLifetime = lifetime;
        body.position = transform.position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    public void OnDespawn()
    {
        launched = false;
        owner = null;
        body.linearVelocity = Vector2.zero;
        hits.Clear();
    }

    public void Launch(Vector2 aim, EnemyBase source)
    {
        owner = source;
        direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
        distanceTravelled = 0f;
        remainingLifetime = lifetime;
        launched = true;
        body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (owner != null)
        {
            Vector2 origin = owner.transform.position;
            Vector2 toMuzzle = body.position - origin;
            // Cover between the enemy center and its muzzle must also stop a shot.
            if (toMuzzle.sqrMagnitude > 0.0001f)
                Sweep(origin, toMuzzle.normalized, toMuzzle.magnitude);
        }
    }

    private void FixedUpdate()
    {
        if (!launched || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;

        remainingLifetime -= Time.fixedDeltaTime;
        if (remainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        float distance = Mathf.Min(speed * Time.fixedDeltaTime, Mathf.Max(0f, maxRange - distanceTravelled));
        Vector2 origin = body.position;
        if (Sweep(origin, direction, distance))
            return;
        body.MovePosition(origin + direction * distance);
        distanceTravelled += distance;
        if (distanceTravelled >= maxRange)
            Despawn();
    }

    private bool Sweep(Vector2 origin, Vector2 heading, float distance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers | blockingLayers);
        filter.useTriggers = true;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 center = origin + (Vector2)transform.TransformVector(hitCollider.offset);
        Physics2D.CircleCast(center, hitCollider.radius * scale, heading, filter, hits, distance);
        Collider2D closest = null;
        PlayerController hitPlayer = null;
        float closestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D other = hit.collider;
            if (other == null || other.attachedRigidbody == body)
                continue;
            if (owner != null && (other.transform == owner.transform || other.transform.IsChildOf(owner.transform)))
                continue;
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null && other.attachedRigidbody != null)
                enemy = other.attachedRigidbody.GetComponent<EnemyBase>();
            if (enemy != null)
                continue;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null && other.attachedRigidbody != null)
                player = other.attachedRigidbody.GetComponent<PlayerController>();
            bool blocks = player == null && !other.isTrigger && (blockingLayers.value & (1 << other.gameObject.layer)) != 0;
            bool damagesPlayer = player != null && (hitLayers.value & (1 << other.gameObject.layer)) != 0;
            if ((!blocks && !damagesPlayer) || hit.distance > closestDistance)
                continue;
            // At equal distance, solid cover wins over a player touching its far side.
            if (hit.distance == closestDistance && hitPlayer == null)
                continue;
            closestDistance = hit.distance;
            closest = other;
            hitPlayer = damagesPlayer && !blocks ? player : null;
        }
        if (closest == null)
            return false;

        int hitSpawnId = spawnId;
        if (hitPlayer != null)
        {
            IDamageable receiver = hitPlayer.GetComponent<IDamageable>();
            receiver?.TakeDamage(new DamageInfo
            {
                amount = damage,
                knockback = heading * knockback,
                fromRecall = false,
                source = gameObject
            });
        }
        if (spawnId == hitSpawnId && launched && isActiveAndEnabled)
            Despawn();
        return true;
    }

    private void Despawn()
    {
        if (PoolManager.Instance != null)
            PoolManager.Instance.Return(gameObject);
        else
        {
            OnDespawn();
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.01f, speed);
        damage = Mathf.Max(1, damage);
        knockback = Mathf.Max(0f, knockback);
        maxRange = Mathf.Max(0.01f, maxRange);
        lifetime = Mathf.Max(0.01f, lifetime);
    }
}
