using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GuardBotAI : EnemyBase
{
    private enum GuardState { Patrol, Chase, Attack, Search }

    [Header("Guard Stats")]
    [SerializeField] private int guardMaxHP = 75;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float searchDuration = 3f;
    [SerializeField] private LayerMask sightBlockingLayers;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolWaypoints = new Transform[0];
    [SerializeField] private float patrolRadius = 2f;
    [SerializeField] private float waypointTolerance = 0.2f;

    [Header("Shooting")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireOffset = 0.45f;
    [SerializeField] private float attackInterval = 1.2f;

    private readonly List<Vector2> patrolPositions = new List<Vector2>();
    private readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>();
    private GuardState state;
    private int patrolIndex;
    private Vector2 lastSeenPosition;
    private Vector2 sightTarget;
    private float searchUntil;
    private float nextAttackAt;

    public override EnemyType Type => EnemyType.GuardBot;
    protected override int SpawnMaxHP => guardMaxHP;
    public string CurrentStateName => state.ToString();
    public bool HasLineOfSight { get; private set; }

    public override void OnSpawn()
    {
        base.OnSpawn();
        state = GuardState.Patrol;
        HasLineOfSight = false;
        lastSeenPosition = Body.position;
        sightTarget = Body.position;
        searchUntil = 0f;
        nextAttackAt = Time.time + attackInterval;
        patrolIndex = 0;
        patrolPositions.Clear();
        if (patrolWaypoints != null)
        {
            foreach (Transform waypoint in patrolWaypoints)
                if (waypoint != null) patrolPositions.Add(waypoint.position);
        }
        if (patrolPositions.Count == 0)
        {
            // Snapshot world destinations so child waypoint markers do not travel with the bot.
            patrolPositions.Add(Body.position + Vector2.right * patrolRadius);
            patrolPositions.Add(Body.position - Vector2.right * patrolRadius);
        }
    }

    public override void OnDespawn()
    {
        HasLineOfSight = false;
        state = GuardState.Patrol;
        patrolPositions.Clear();
        base.OnDespawn();
    }

    protected override void FixedUpdate()
    {
        if (CanAct)
            UpdatePerception();
        base.FixedUpdate();
    }

    private void UpdatePerception()
    {
        bool targetAlive = Target != null && Target.Health != null && !Target.Health.IsDead;
        HasLineOfSight = false;
        if (targetAlive)
        {
            sightTarget = Target.transform.position;
            Vector2 toPlayer = sightTarget - Body.position;
            if (toPlayer.sqrMagnitude <= detectionRange * detectionRange)
                HasLineOfSight = IsSightClear(toPlayer);
        }

        if (HasLineOfSight)
        {
            lastSeenPosition = sightTarget;
            state = (lastSeenPosition - Body.position).sqrMagnitude <= attackRange * attackRange
                ? GuardState.Attack : GuardState.Chase;
        }
        else if (state == GuardState.Attack || state == GuardState.Chase)
        {
            state = GuardState.Search;
            searchUntil = Time.time + searchDuration;
        }
        else if (state == GuardState.Search && Time.time >= searchUntil)
        {
            state = GuardState.Patrol;
        }
    }

    private bool IsSightClear(Vector2 toPlayer)
    {
        float distance = toPlayer.magnitude;
        if (distance <= 0.0001f)
            return true;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(sightBlockingLayers);
        filter.useTriggers = false;
        Physics2D.Raycast(Body.position, toPlayer / distance, filter, sightHits, distance);
        foreach (RaycastHit2D hit in sightHits)
        {
            Collider2D other = hit.collider;
            if (other == null || other.attachedRigidbody == Body || other.transform.IsChildOf(transform))
                continue;
            if (other.transform == Target.transform || other.transform.IsChildOf(Target.transform))
                continue;
            return false;
        }
        return true;
    }

    protected override void TickMovement()
    {
        switch (state)
        {
            case GuardState.Patrol:
                Patrol();
                break;
            case GuardState.Chase:
                Move(lastSeenPosition - Body.position);
                break;
            case GuardState.Attack:
                StopMotion();
                TryShoot();
                break;
            case GuardState.Search:
                Vector2 toLastSeen = lastSeenPosition - Body.position;
                if (toLastSeen.sqrMagnitude <= waypointTolerance * waypointTolerance)
                    StopMotion();
                else
                    Move(toLastSeen);
                break;
        }
    }

    private void Patrol()
    {
        if (patrolPositions.Count == 0)
        {
            StopMotion();
            return;
        }
        Vector2 toWaypoint = patrolPositions[patrolIndex] - Body.position;
        if (toWaypoint.sqrMagnitude <= waypointTolerance * waypointTolerance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPositions.Count;
            toWaypoint = patrolPositions[patrolIndex] - Body.position;
        }
        Move(toWaypoint);
    }

    private void TryShoot()
    {
        if (!HasLineOfSight || Target == null || Time.time < nextAttackAt)
            return;
        nextAttackAt = Time.time + attackInterval;
        if (projectilePrefab == null || PoolManager.Instance == null)
            return;

        Vector2 direction = lastSeenPosition - Body.position;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;
        direction.Normalize();
        Vector2 position = firePoint != null ? (Vector2)firePoint.position : Body.position + direction * fireOffset;
        GameObject instance = PoolManager.Instance.Get(projectilePrefab, position, Quaternion.identity);
        if (instance == null)
            return;
        EnemyProjectile projectile = instance.GetComponent<EnemyProjectile>();
        if (projectile == null)
        {
            PoolManager.Instance.Return(instance);
            return;
        }
        Vector2 shotDirection = lastSeenPosition - position;
        projectile.Launch(shotDirection.sqrMagnitude > 0.0001f ? shotDirection : direction, this);
    }

    private void OnDrawGizmos()
    {
        Vector2 origin = transform.position;
        Vector2 destination = Application.isPlaying ? sightTarget
            : Target != null ? (Vector2)Target.transform.position : origin + Vector2.right * detectionRange;
        Gizmos.color = HasLineOfSight ? Color.green : Color.red;
        Gizmos.DrawLine(origin, destination);
        Gizmos.DrawWireSphere(origin, attackRange);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        guardMaxHP = Mathf.Max(1, guardMaxHP);
        attackRange = Mathf.Max(0.1f, attackRange);
        detectionRange = Mathf.Max(attackRange, detectionRange);
        searchDuration = Mathf.Max(0f, searchDuration);
        patrolRadius = Mathf.Max(0f, patrolRadius);
        waypointTolerance = Mathf.Max(0.01f, waypointTolerance);
        fireOffset = Mathf.Max(0f, fireOffset);
        attackInterval = Mathf.Max(0.01f, attackInterval);
    }
}
