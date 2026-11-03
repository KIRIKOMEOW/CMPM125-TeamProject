using UnityEngine;
using UnityEngine.UI;

// Step-by-step prompts during wave 1: throw, recall, pierce, barrels. Advances on real game events.
public class TutorialHints : MonoBehaviour
{
    private const string Pref = "NightShift.TutorialHints";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text messageText;
    [SerializeField] private Text keyText;
    [SerializeField] private float fadeSpeed = 4f;

    private enum Step { Off, Throw, Recall, Pierce, Barrel, Done }

    private Step step = Step.Off;
    private int throwsThisStep;
    private float stepTimer;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Pref, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(Pref, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveCleared += OnWaveCleared;
        GameEvents.BladeThrown += OnBladeThrown;
        GameEvents.RecallStarted += OnRecallStarted;
        GameEvents.RunReset += OnRunReset;
        Loc.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveCleared -= OnWaveCleared;
        GameEvents.BladeThrown -= OnBladeThrown;
        GameEvents.RecallStarted -= OnRecallStarted;
        GameEvents.RunReset -= OnRunReset;
        Loc.Changed -= Refresh;
    }

    private void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        bool visible = playing && step != Step.Off && step != Step.Done;
        if (group != null)
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);

        if (!playing)
            return;

        // Timed steps advance on their own once the player has read them.
        if (step == Step.Pierce || step == Step.Barrel)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
                SetStep(step == Step.Pierce ? Step.Barrel : Step.Done);
        }
    }

    private void OnWaveStarted(int wave, int total)
    {
        if (wave == 1 && Enabled)
            SetStep(Step.Throw);
        else if (step != Step.Off)
            SetStep(Step.Done);
    }

    private void OnWaveCleared(int wave) => SetStep(Step.Done);
    private void OnRunReset() => SetStep(Step.Off);

    private void OnBladeThrown(Vector2 position)
    {
        if (step != Step.Throw) return;
        if (++throwsThisStep >= 2)
            SetStep(Step.Recall);
    }

    private void OnRecallStarted()
    {
        if (step == Step.Recall || step == Step.Throw)
            SetStep(Step.Pierce);
    }

    private void SetStep(Step next)
    {
        step = next;
        throwsThisStep = 0;
        stepTimer = next == Step.Pierce ? 4f : next == Step.Barrel ? 5f : 0f;
        Refresh();
    }

    private void Refresh()
    {
        if (messageText == null) return;
        messageText.font = Loc.Font;
        switch (step)
        {
            case Step.Throw: Show("tut.throw", "LMB"); break;
            case Step.Recall: Show("tut.recall", "E"); break;
            case Step.Pierce: Show("tut.pierce", "!"); break;
            case Step.Barrel: Show("tut.barrel", "!"); break;
        }
    }

    private void Show(string key, string keyCap)
    {
        messageText.text = Loc.Get(key);
        if (keyText != null) keyText.text = keyCap;
    }
}
