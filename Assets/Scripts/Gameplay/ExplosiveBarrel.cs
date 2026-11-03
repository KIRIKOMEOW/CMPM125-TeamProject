using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ExplosiveBarrel : MonoBehaviour, IDamageable
{
    [Header("Explosion")]
    [SerializeField] private int hitsToExplode = 2;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int explosionDamage = 60;
    [SerializeField] private float explosionForce = 12f;
    [SerializeField] private float chainReactionDelay = 0.08f;
    [SerializeField] private LayerMask affectedLayers = ~0;

    private Rigidbody2D body;
    private Collider2D[] colliders;
    private Renderer[] renderers;
    private bool[] colliderStates;
    private bool[] rendererStates;
    private Vector2 initialPosition;
    private float initialRotation;
    private bool initialSimulation;
    private Coroutine chainReactionRoutine;

    public bool IsExploded { get; private set; }
    public int HitsRemaining { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        GetComponent<CircleCollider2D>().isTrigger = false;
        initialPosition = transform.position;
        initialRotation = transform.eulerAngles.z;
        initialSimulation = body.simulated;
        colliders = GetComponentsInChildren<Collider2D>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        colliderStates = new bool[colliders.Length];
        rendererStates = new bool[renderers.Length];
        for (int i = 0; i < colliders.Length; i++)
            colliderStates[i] = colliders[i].enabled;
        for (int i = 0; i < renderers.Length; i++)
            rendererStates[i] = renderers[i].enabled;
        HitsRemaining = hitsToExplode;
    }

    private void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.RunReset += ResetBarrel;
        GameEvents.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.RunReset -= ResetBarrel;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        CancelChainReaction();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!isActiveAndEnabled || IsExploded || chainReactionRoutine != null || info.amount <= 0 || GameManager.Instance == null
            || GameManager.Instance.State != GameState.Playing)
            return;

        ExplosiveBarrel sourceBarrel = info.source != null ? info.source.GetComponent<ExplosiveBarrel>() : null;
        if (sourceBarrel != null && sourceBarrel != this)
        {
            chainReactionRoutine = StartCoroutine(ChainReaction());
            return;
        }

        HitsRemaining = Mathf.Max(0, HitsRemaining - 1);
        if (HitsRemaining == 0)
            Explode();
    }

    private IEnumerator ChainReaction()
    {
        yield return new WaitForSeconds(chainReactionDelay);
        chainReactionRoutine = null;
        if (isActiveAndEnabled && !IsExploded && GameManager.Instance != null
            && GameManager.Instance.State == GameState.Playing)
            Explode();
    }

    private void CancelChainReaction()
    {
        if (chainReactionRoutine != null)
            StopCoroutine(chainReactionRoutine);
        chainReactionRoutine = null;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state != GameState.Playing)
            CancelChainReaction();
    }

    private void Explode()
    {
        GameManager manager = GameManager.Instance;
        // A lethal player hit cannot cancel the remaining targets within this blast.
        manager.BeginDamageBatch();
        try
        {
            ApplyExplosion();
        }
        finally
        {
            if (manager != null) manager.EndDamageBatch();
        }
    }

    private void ApplyExplosion()
    {
        IsExploded = true;
        HitsRemaining = 0;
        Vector2 center = transform.position;
        SetVisible(false);
        body.simulated = false;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(center, explosionRadius, affectedLayers);
        HashSet<Rigidbody2D> affectedBodies = new HashSet<Rigidbody2D>();
        HashSet<Component> damageTargets = new HashSet<Component>();
        foreach (Collider2D nearbyCollider in nearby)
        {
            if (nearbyCollider == null || nearbyCollider.transform.IsChildOf(transform))
                continue;

            Rigidbody2D nearbyBody = nearbyCollider.attachedRigidbody;
            if (nearbyBody != null && nearbyBody != body && affectedBodies.Add(nearbyBody))
            {
                Vector2 away = nearbyBody.worldCenterOfMass - center;
                Vector2 impulse = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up) * explosionForce;
                PlayerController player = nearbyBody.GetComponent<PlayerController>();
                EnemyBase enemy = nearbyBody.GetComponent<EnemyBase>();
                if (player != null)
                    player.ApplyKnockback(impulse);
                else if (enemy != null)
                    enemy.ApplyKnockback(impulse);
                else
                    nearbyBody.AddForce(impulse, ForceMode2D.Impulse);
            }

            IDamageable damageable = nearbyCollider.GetComponent<IDamageable>();
            if (damageable == null && nearbyBody != null)
                damageable = nearbyBody.GetComponent<IDamageable>();
            if (damageable is Component receiver && receiver != this)
                damageTargets.Add(receiver);
        }

        // Damage and physics are separate: i-frames must not make bodies immune to the blast force.
        foreach (Component receiver in damageTargets)
        {
            if (receiver != null && receiver.gameObject.activeInHierarchy && receiver is IDamageable damageable)
            {
                damageable.TakeDamage(new DamageInfo
                {
                    amount = explosionDamage,
                    knockback = Vector2.zero,
                    fromRecall = false,
                    source = gameObject
                });
            }
        }
        GameEvents.RaiseBarrelExploded(center);
    }

    public void ResetBarrel()
    {
        CancelChainReaction();
        IsExploded = false;
        HitsRemaining = hitsToExplode;
        body.position = initialPosition;
        body.rotation = initialRotation;
        body.simulated = initialSimulation;
        SetVisible(true);
    }

    private void OnWaveStarted(int wave, int totalWaves)
    {
        ResetBarrel();
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = visible && colliderStates[i];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = visible && rendererStates[i];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    private void OnValidate()
    {
        hitsToExplode = Mathf.Max(1, hitsToExplode);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionDamage = Mathf.Max(1, explosionDamage);
        explosionForce = Mathf.Max(0f, explosionForce);
        chainReactionDelay = Mathf.Max(0.01f, chainReactionDelay);
    }
}
