using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Blade : MonoBehaviour, IPoolable
{
    [Header("Flight")]
    [SerializeField] private float flightSpeed = 18f;
    [SerializeField] private float returnSpeed = 24f;
    [SerializeField] private float flightRange = 10f;
    [SerializeField] private float catchDistance = 0.25f;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private LayerMask hitLayers = ~0;

    private enum FlightState { Inactive, Outbound, Resting, Returning }

    private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();
    private readonly Dictionary<Component, int> hitTargets = new Dictionary<Component, int>();
    private Rigidbody2D body;
    private CircleCollider2D hitCollider;
    private PlayerController owner;
    private BladeRecall.RecallVolley volley;
    private FlightState state;
    private Vector2 direction;
    private float distanceTravelled;
    private int damage;
    private float knockback;

    public bool CanRecall => isActiveAndEnabled && (state == FlightState.Outbound || state == FlightState.Resting);
    public bool IsReturning => state == FlightState.Returning;

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
        state = FlightState.Inactive;
        owner = null;
        volley = null;
        hitTargets.Clear();
        distanceTravelled = 0f;
        body.position = transform.position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    public void OnDespawn()
    {
        if (owner != null) owner.OnBladeDespawned(this);
        owner = null;
        volley = null;
        state = FlightState.Inactive;
        hitTargets.Clear();
        body.linearVelocity = Vector2.zero;
    }

    internal void Launch(PlayerController player, Vector2 aim, int hitDamage, float hitKnockback)
    {
        owner = player;
        direction = aim.sqrMagnitude > 0f ? aim.normalized : Vector2.right;
        damage = hitDamage;
        knockback = hitKnockback;
        distanceTravelled = 0f;
        hitTargets.Clear();
        body.position = transform.position;
        body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        state = ClampMuzzleToWall() ? FlightState.Resting : FlightState.Outbound;
    }

    private bool ClampMuzzleToWall()
    {
        Vector2 playerPosition = owner.transform.position;
        Vector2 toMuzzle = body.position - playerPosition;
        float distance = toMuzzle.magnitude;
        if (distance <= 0.0001f) return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(wallLayers);
        filter.useTriggers = false;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 center = playerPosition + (Vector2)transform.TransformVector(hitCollider.offset);
        Physics2D.CircleCast(center, hitCollider.radius * scale, toMuzzle.normalized, filter, castHits, distance);
        bool hitWall = false;
        foreach (RaycastHit2D hit in castHits)
        {
            if (IsIgnored(hit.collider) || hit.distance > distance) continue;
            distance = hit.distance;
            hitWall = true;
        }
        // Check the muzzle offset as well as flight, so a close wall cannot be skipped at launch.
        if (hitWall) body.position = playerPosition + toMuzzle.normalized * distance;
        return hitWall;
    }

    internal void BeginRecall(BladeRecall.RecallVolley recallVolley)
    {
        if (!CanRecall || owner == null) return;
        volley = recallVolley;
        hitTargets.Clear();
        state = FlightState.Returning;
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        if (state != FlightState.Outbound && state != FlightState.Returning) return;
        if (owner == null)
        {
            Despawn();
            return;
        }

        Vector2 origin = body.position;
        float travel;
        bool returning = state == FlightState.Returning;
        if (returning)
        {
            Vector2 toPlayer = (Vector2)owner.transform.position - origin;
            if (toPlayer.magnitude <= catchDistance)
            {
                CompleteReturn();
                return;
            }
            direction = toPlayer.normalized;
            travel = Mathf.Min(returnSpeed * Time.fixedDeltaTime, toPlayer.magnitude);
        }
        else
        {
            travel = Mathf.Min(flightSpeed * Time.fixedDeltaTime, Mathf.Max(0f, flightRange - distanceTravelled));
        }

        float permittedTravel = Sweep(origin, direction, travel, returning, out bool hitWall);
        if (!isActiveAndEnabled || state == FlightState.Inactive) return;
        body.MovePosition(origin + direction * permittedTravel);
        if (returning)
        {
            if (Vector2.Distance(origin + direction * permittedTravel, owner.transform.position) <= catchDistance)
                CompleteReturn();
        }
        else
        {
            distanceTravelled += permittedTravel;
            if (hitWall || distanceTravelled >= flightRange - 0.001f) state = FlightState.Resting;
        }
    }

    private float Sweep(Vector2 origin, Vector2 heading, float distance, bool returning, out bool hitWall)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers | wallLayers);
        filter.useTriggers = true;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        float radius = hitCollider.radius * scale;
        Vector2 center = origin + (Vector2)transform.TransformVector(hitCollider.offset);
        Physics2D.CircleCast(center, radius, heading, filter, castHits, distance);
        float allowedDistance = distance;
        hitWall = false;
        if (!returning)
        {
            foreach (RaycastHit2D hit in castHits)
            {
                if (IsIgnored(hit.collider)) continue;
                if ((wallLayers.value & (1 << hit.collider.gameObject.layer)) == 0 || hit.collider.isTrigger) continue;
                if (hit.distance <= allowedDistance)
                {
                    allowedDistance = hit.distance;
                    hitWall = true;
                }
            }
        }

        foreach (RaycastHit2D hit in castHits)
        {
            if (hit.distance > allowedDistance + 0.001f || IsIgnored(hit.collider)) continue;
            if ((hitLayers.value & (1 << hit.collider.gameObject.layer)) == 0) continue;
            DealDamage(hit.collider, heading, returning);
        }
        return allowedDistance;
    }

    private bool IsIgnored(Collider2D other)
    {
        return other == null || other.attachedRigidbody == body
            || (owner != null && (other.transform == owner.transform || other.transform.IsChildOf(owner.transform)));
    }

    private void DealDamage(Collider2D other, Vector2 heading, bool returning)
    {
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null && other.attachedRigidbody != null)
            target = other.attachedRigidbody.GetComponent<IDamageable>();
        Component receiver = target as Component;
        if (receiver == null) return;
        EnemyBase enemy = receiver.GetComponent<EnemyBase>();
        int spawnId = enemy != null ? enemy.SpawnId : 0;
        if (hitTargets.TryGetValue(receiver, out int previousSpawn) && previousSpawn == spawnId) return;
        hitTargets[receiver] = spawnId;
        target.TakeDamage(new DamageInfo
        {
            amount = damage,
            knockback = heading * knockback,
            fromRecall = returning,
            source = gameObject
        });
        if (returning && volley != null && enemy != null) volley.RegisterHit(enemy, spawnId);
    }

    private void CompleteReturn()
    {
        PlayerController previousOwner = owner;
        owner = null;
        if (previousOwner != null) previousOwner.OnBladeReturned(this);
        Despawn();
    }

    private void Despawn()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Return(gameObject);
        else
        {
            OnDespawn();
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        flightSpeed = Mathf.Max(0.01f, flightSpeed);
        returnSpeed = Mathf.Max(0.01f, returnSpeed);
        flightRange = Mathf.Max(0f, flightRange);
        catchDistance = Mathf.Max(0.01f, catchDistance);
    }
}
