using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HitStop : MonoBehaviour
{
    private Coroutine stopRoutine;
    private float stopUntil;
    private bool ownsFreeze;

    public static HitStop Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;
        GameFeel.EnabledChanged += OnFeelChanged;
        GameEvents.GameStateChanged += OnGameStateChanged;
        GameEvents.RunReset += CancelStop;
    }

    private void OnDisable()
    {
        GameFeel.EnabledChanged -= OnFeelChanged;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.RunReset -= CancelStop;
        CancelStop();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Stop(float duration)
    {
        GameManager manager = GameManager.Instance;
        if (!isActiveAndEnabled || Instance != this || !GameFeel.Enabled || duration <= 0f
            || manager == null || manager.State != GameState.Playing)
            return;

        stopUntil = Mathf.Max(stopUntil, Time.realtimeSinceStartup + duration);
        ownsFreeze = true;
        Time.timeScale = 0f;
        if (stopRoutine == null)
            stopRoutine = StartCoroutine(WaitForStop());
    }

    private IEnumerator WaitForStop()
    {
        while (ownsFreeze)
        {
            float remaining = stopUntil - Time.realtimeSinceStartup;
            if (remaining <= 0f)
                break;
            yield return new WaitForSecondsRealtime(remaining);
        }

        stopRoutine = null;
        ReleaseFreeze();
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state != GameState.Playing)
            CancelStop();
    }

    private void OnFeelChanged(bool enabled)
    {
        if (!enabled)
            CancelStop();
    }

    private void CancelStop()
    {
        if (stopRoutine != null)
            StopCoroutine(stopRoutine);
        stopRoutine = null;
        ReleaseFreeze();
    }

    private void ReleaseFreeze()
    {
        if (!ownsFreeze)
            return;

        ownsFreeze = false;
        stopUntil = 0f;
        // A menu or pause now owns timeScale; a delayed recovery must not undo it.
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            Time.timeScale = 1f;
    }
}
