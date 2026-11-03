using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    [Header("Shake Target")]
    [SerializeField] private Transform shakeTarget;

    private Coroutine shakeRoutine;
    private Vector3 appliedOffset;
    private float shakeUntil;
    private float fadeDuration;
    private float strength;

    public static CameraShake Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (shakeTarget == null)
            shakeTarget = transform;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;
        GameFeel.EnabledChanged += OnFeelChanged;
        GameEvents.RunReset += CancelShake;
    }

    private void OnDisable()
    {
        GameFeel.EnabledChanged -= OnFeelChanged;
        GameEvents.RunReset -= CancelShake;
        CancelShake();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Shake(float intensity, float duration)
    {
        if (!isActiveAndEnabled || Instance != this || !GameFeel.Enabled
            || shakeTarget == null || intensity <= 0f || duration <= 0f)
            return;

        float now = Time.unscaledTime;
        strength = Mathf.Max(strength, intensity);
        shakeUntil = Mathf.Max(shakeUntil, now + duration);
        fadeDuration = shakeUntil - now;
        if (shakeRoutine == null)
            shakeRoutine = StartCoroutine(ShakeDuration());
    }

    private IEnumerator ShakeDuration()
    {
        while (Time.unscaledTime < shakeUntil && GameFeel.Enabled)
            yield return null;

        shakeRoutine = null;
        ClearShake();
    }

    private void LateUpdate()
    {
        if (shakeRoutine == null)
            return;
        if (shakeTarget == null)
        {
            CancelShake();
            return;
        }

        RemoveOffset();
        float fade = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / Mathf.Max(0.0001f, fadeDuration));
        Vector2 random = Random.insideUnitCircle * strength * fade;
        appliedOffset = new Vector3(Mathf.Round(random.x * 32f) / 32f, Mathf.Round(random.y * 32f) / 32f, 0f);
        shakeTarget.localPosition += appliedOffset;
    }

    private void OnFeelChanged(bool enabled)
    {
        if (!enabled)
            CancelShake();
    }

    private void CancelShake()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = null;
        ClearShake();
    }

    private void ClearShake()
    {
        RemoveOffset();
        strength = 0f;
        shakeUntil = 0f;
        fadeDuration = 0f;
    }

    private void RemoveOffset()
    {
        // Remove only this effect's local offset, preserving motion of the camera rig.
        if (shakeTarget != null)
            shakeTarget.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;
    }
}
