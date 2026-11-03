using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int initialMaxHP = 100;
    [SerializeField] private bool isPlayer;
    [SerializeField] private float invincibilityDuration = 0.6f;

    [Header("Hit Feedback")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private float blinkInterval = 0.075f;

    private Material[] originalMaterials;
    private Color[] originalColors;
    private Coroutine invincibilityRoutine;
    private Coroutine flashRoutine;
    private bool initialized;
    private bool invulnerable;
    private bool flashing;
    private bool blinking;
    private int currentHP;
    private int maximumHP;

    public int CurrentHP
    {
        get
        {
            Initialize();
            return currentHP;
        }
        private set => currentHP = value;
    }

    public int MaxHP
    {
        get
        {
            Initialize();
            return maximumHP;
        }
        private set => maximumHP = value;
    }
    public bool IsDead => CurrentHP <= 0;
    public bool IsInvulnerable => invulnerable;

    public event Action<DamageInfo> Damaged;
    public event Action Died;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        GameFeel.EnabledChanged += OnFeelEnabledChanged;
    }

    private void OnDisable()
    {
        GameFeel.EnabledChanged -= OnFeelEnabledChanged;
        ClearFeedback();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        isPlayer = isPlayer || GetComponent<PlayerController>() != null;
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        originalMaterials = new Material[spriteRenderers.Length];
        originalColors = new Color[spriteRenderers.Length];
        MaxHP = Mathf.Max(1, initialMaxHP);
        CurrentHP = MaxHP;
    }

    public bool ApplyDamage(DamageInfo info)
    {
        Initialize();
        if (!isActiveAndEnabled || IsDead || invulnerable || info.amount <= 0)
            return false;

        CurrentHP = Mathf.Max(0, CurrentHP - info.amount);
        if (isPlayer && !IsDead && invincibilityDuration > 0f)
            invincibilityRoutine = StartCoroutine(Invincibility());

        StartFlash();
        Damaged?.Invoke(info);
        if (isPlayer)
        {
            GameEvents.RaisePlayerHealthChanged(CurrentHP, MaxHP);
            GameEvents.RaisePlayerHit(transform.position);
        }

        if (IsDead)
            Died?.Invoke();

        return true;
    }

    public void Heal(int amount)
    {
        Initialize();
        if (amount <= 0 || IsDead)
            return;

        CurrentHP += Mathf.Min(amount, MaxHP - CurrentHP);
        PublishStatus();
    }

    public void ResetHealth(int maxHP)
    {
        Initialize();
        ClearFeedback();
        MaxHP = Mathf.Max(1, maxHP);
        CurrentHP = MaxHP;
        PublishStatus();
    }

    public void PublishStatus()
    {
        Initialize();
        if (isPlayer)
            GameEvents.RaisePlayerHealthChanged(CurrentHP, MaxHP);
    }

    private IEnumerator Invincibility()
    {
        invulnerable = true;
        blinking = true;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }

        float elapsed = 0f;
        while (elapsed < invincibilityDuration)
        {
            bool dimmed = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, blinkInterval)) % 2 == 0;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null)
                    continue;

                Color color = originalColors[i];
                if (dimmed)
                    color.a *= 0.35f;
                spriteRenderers[i].color = color;
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        RestoreColors();
        invulnerable = false;
        invincibilityRoutine = null;
    }

    private void StartFlash()
    {
        if (!GameFeel.Enabled || flashMaterial == null || flashDuration <= 0f)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        RestoreMaterials();
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        flashing = true;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            originalMaterials[i] = spriteRenderers[i].sharedMaterial;
            spriteRenderers[i].sharedMaterial = flashMaterial;
        }

        yield return new WaitForSeconds(flashDuration);
        RestoreMaterials();
        flashRoutine = null;
    }

    private void OnFeelEnabledChanged(bool enabled)
    {
        if (enabled) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
        RestoreMaterials();
    }

    private void ClearFeedback()
    {
        if (invincibilityRoutine != null)
            StopCoroutine(invincibilityRoutine);
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        invincibilityRoutine = null;
        flashRoutine = null;
        invulnerable = false;
        RestoreColors();
        RestoreMaterials();
    }

    private void RestoreColors()
    {
        if (!blinking)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
        blinking = false;
    }

    private void RestoreMaterials()
    {
        if (!flashing)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].sharedMaterial = originalMaterials[i];
        }
        flashing = false;
    }

    private void OnValidate()
    {
        initialMaxHP = Mathf.Max(1, initialMaxHP);
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        flashDuration = Mathf.Max(0f, flashDuration);
        blinkInterval = Mathf.Max(0.01f, blinkInterval);
    }
}
