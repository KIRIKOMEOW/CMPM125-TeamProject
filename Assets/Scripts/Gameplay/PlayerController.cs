using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(BladeRecall))]
public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float accelerationTime = 0.1f;
    [SerializeField] private float linearDamping = 10f;
    [SerializeField] private float knockbackDuration = 0.12f;

    [Header("Blades")]
    [SerializeField] private GameObject bladePrefab;
    [SerializeField] private int maxBlades = 5;
    [SerializeField] private int bladeDamage = 25;
    [SerializeField] private float bladeKnockback = 4f;
    [SerializeField] private float throwInterval = 0.3f;
    [SerializeField] private float spawnOffset = 0.45f;

    [Header("Upgrade Effects")]
    [SerializeField] private float throwRatePerLevel = 0.15f;
    [SerializeField] private int damagePerLevel = 10;
    [SerializeField] private int maxHPPerLevel = 25;
    [SerializeField] private float moveSpeedPerLevel = 0.1f;

    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform throwPoint;

    private readonly HashSet<Blade> activeBlades = new HashSet<Blade>();
    private readonly List<Blade> resetBlades = new List<Blade>();
    private Rigidbody2D body;
    private BladeRecall recall;
    private Vector2 spawnPosition;
    private Vector2 moveInput;
    private float throwRemaining;
    private float knockbackRemaining;
    private int startingMaxHP;
    private float startingMoveSpeed;
    private float startingThrowInterval;
    private int startingBladeDamage;
    private int startingMaxBlades;
    private bool hasSpeedParameter;
    private bool hasThrowParameter;
    private bool hasHurtParameter;
    private bool waitForMouseRelease = true;

    public Health Health { get; private set; }
    public int BladesInHand { get; private set; }
    public int MaxBlades => maxBlades;
    public float MoveSpeed => moveSpeed;
    public float ThrowInterval => throwInterval;
    public int BladeDamage => bladeDamage;
    public bool FanThrowAvailable => false;
    public bool FanThrowUnlocked { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private bool IsPlaying => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        Health = GetComponent<Health>();
        recall = GetComponent<BladeRecall>();
        spawnPosition = transform.position;
        startingMaxHP = Health.MaxHP;
        startingMoveSpeed = moveSpeed;
        startingThrowInterval = throwInterval;
        startingBladeDamage = bladeDamage;
        startingMaxBlades = maxBlades;
        BladesInHand = maxBlades;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.linearDamping = linearDamping;
        if (aimCamera == null) aimCamera = Camera.main;
        if (bodySprite == null) bodySprite = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        Health.Damaged += OnDamaged;
        GameEvents.GameStateChanged += OnGameStateChanged;
        GameEvents.RunReset += ResetUpgrades;
        waitForMouseRelease = true;
    }

    private void OnDisable()
    {
        Health.Damaged -= OnDamaged;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.RunReset -= ResetUpgrades;
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (!IsPlaying || Health.IsDead)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        UpdateAim();
        throwRemaining = Mathf.Max(0f, throwRemaining - Time.deltaTime);
        HandleThrowInput(Input.GetMouseButton(0));
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)) recall.TryRecall();
        if (hasSpeedParameter) animator.SetFloat("Speed", moveInput.magnitude);
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            waitForMouseRelease = true;
    }

    private void HandleThrowInput(bool leftMouseHeld)
    {
        // A menu click must finish before it can become a gameplay attack.
        if (waitForMouseRelease)
        {
            if (!leftMouseHeld) waitForMouseRelease = false;
            return;
        }
        if (leftMouseHeld) TryThrow();
    }

    private void FixedUpdate()
    {
        if (!IsPlaying || Health.IsDead) return;
        if (knockbackRemaining > 0f)
        {
            knockbackRemaining -= Time.fixedDeltaTime;
            return;
        }

        if (moveInput.sqrMagnitude < 0.001f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desiredVelocity = moveInput * moveSpeed;
        float acceleration = moveSpeed / Mathf.Max(0.001f, accelerationTime);
        // Compensate damping while walking so the configured speed remains the actual speed.
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime)
            * (1f + body.linearDamping * Time.fixedDeltaTime);
    }

    private void UpdateAim()
    {
        if (aimCamera == null) return;
        Ray mouseRay = aimCamera.ScreenPointToRay(Input.mousePosition);
        Plane aimPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        if (!aimPlane.Raycast(mouseRay, out float distance)) return;
        Vector2 direction = (Vector2)mouseRay.GetPoint(distance) - body.position;
        if (direction.sqrMagnitude < 0.0001f) return;
        AimDirection = direction.normalized;
        if (bodySprite != null) bodySprite.flipX = AimDirection.x < 0f;
        if (aimPivot != null && aimPivot != transform)
            aimPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
    }

    public bool TryThrow()
    {
        if (!IsPlaying || Health.IsDead || BladesInHand <= 0 || throwRemaining > 0f || bladePrefab == null || PoolManager.Instance == null)
            return false;

        Vector2 position = throwPoint != null ? (Vector2)throwPoint.position : body.position + AimDirection * spawnOffset;
        GameObject instance = PoolManager.Instance.Get(bladePrefab, position, Quaternion.identity);
        if (instance == null) return false;
        Blade blade = instance.GetComponent<Blade>();
        if (blade == null)
        {
            PoolManager.Instance.Return(instance);
            return false;
        }

        activeBlades.Add(blade);
        BladesInHand--;
        throwRemaining = throwInterval;
        blade.Launch(this, AimDirection, bladeDamage, bladeKnockback);
        GameEvents.RaiseBladesChanged(BladesInHand, maxBlades);
        GameEvents.RaiseBladeThrown(blade.transform.position);
        if (hasThrowParameter) animator.SetTrigger("Throw");
        return true;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsPlaying) return;
        Health.ApplyDamage(info);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (info.knockback.sqrMagnitude > 0f)
        {
            body.linearVelocity = Vector2.zero;
            body.AddForce(info.knockback, ForceMode2D.Impulse);
            knockbackRemaining = knockbackDuration;
        }
        if (hasHurtParameter) animator.SetTrigger("Hurt");
    }

    internal void CopyRecallableBlades(List<Blade> destination)
    {
        destination.Clear();
        foreach (Blade blade in activeBlades)
            if (blade != null && blade.CanRecall) destination.Add(blade);
    }

    internal void OnBladeReturned(Blade blade)
    {
        if (!activeBlades.Remove(blade)) return;
        BladesInHand = Mathf.Min(maxBlades, BladesInHand + 1);
        GameEvents.RaiseBladesChanged(BladesInHand, maxBlades);
    }

    internal void OnBladeDespawned(Blade blade)
    {
        activeBlades.Remove(blade);
    }

    public void ResetForNewRun()
    {
        ResetUpgrades();
        recall.ResetRecall();
        resetBlades.Clear();
        resetBlades.AddRange(activeBlades);
        foreach (Blade blade in resetBlades)
        {
            if (blade == null) continue;
            if (PoolManager.Instance != null) PoolManager.Instance.Return(blade.gameObject);
            else blade.gameObject.SetActive(false);
        }
        resetBlades.Clear();
        activeBlades.Clear();
        BladesInHand = maxBlades;
        moveInput = Vector2.zero;
        throwRemaining = 0f;
        knockbackRemaining = 0f;
        waitForMouseRelease = true;
        AimDirection = Vector2.right;
        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        if (bodySprite != null) bodySprite.flipX = false;
        if (aimPivot != null && aimPivot != transform) aimPivot.rotation = Quaternion.identity;
        Health.ResetHealth(startingMaxHP);
        if (hasSpeedParameter) animator.SetFloat("Speed", 0f);
        if (hasThrowParameter) animator.ResetTrigger("Throw");
        if (hasHurtParameter) animator.ResetTrigger("Hurt");
        PublishStatus();
    }

    public void ApplyUpgrades(int throwRateLevel, int damageLevel, int maxHPLevel, int moveSpeedLevel, bool fanUnlocked)
    {
        throwInterval = startingThrowInterval / (1f + Mathf.Clamp(throwRateLevel, 0, 3) * throwRatePerLevel);
        bladeDamage = startingBladeDamage + Mathf.Clamp(damageLevel, 0, 3) * damagePerLevel;
        moveSpeed = startingMoveSpeed * (1f + Mathf.Clamp(moveSpeedLevel, 0, 2) * moveSpeedPerLevel);
        FanThrowUnlocked = fanUnlocked && FanThrowAvailable;
        int upgradedMaxHP = startingMaxHP + Mathf.Clamp(maxHPLevel, 0, 3) * maxHPPerLevel;
        // Reapplying damage/rate/speed upgrades must not heal an injured player.
        if (Health.MaxHP != upgradedMaxHP)
            Health.ResetHealth(upgradedMaxHP);
    }

    private void ResetUpgrades()
    {
        ApplyUpgrades(0, 0, 0, 0, false);
        maxBlades = startingMaxBlades;
    }

    public void PublishStatus()
    {
        GameEvents.RaisePlayerHealthChanged(Health.CurrentHP, Health.MaxHP);
        GameEvents.RaiseBladesChanged(BladesInHand, maxBlades);
        recall.PublishStatus();
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) hasSpeedParameter = true;
            if (parameter.name == "Throw" && parameter.type == AnimatorControllerParameterType.Trigger) hasThrowParameter = true;
            if (parameter.name == "Hurt" && parameter.type == AnimatorControllerParameterType.Trigger) hasHurtParameter = true;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        accelerationTime = Mathf.Max(0.001f, accelerationTime);
        linearDamping = Mathf.Max(0f, linearDamping);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        maxBlades = Mathf.Max(1, maxBlades);
        bladeDamage = Mathf.Max(0, bladeDamage);
        bladeKnockback = Mathf.Max(0f, bladeKnockback);
        throwInterval = Mathf.Max(0.01f, throwInterval);
        spawnOffset = Mathf.Max(0f, spawnOffset);
        throwRatePerLevel = Mathf.Max(0f, throwRatePerLevel);
        damagePerLevel = Mathf.Max(0, damagePerLevel);
        maxHPPerLevel = Mathf.Max(0, maxHPPerLevel);
        moveSpeedPerLevel = Mathf.Max(0f, moveSpeedPerLevel);
    }
}
