using UnityEngine;

// Shows the Guard Bot / Brute state machine above the enemy while the F2 debug overlay is on.
public class EnemyStateLabel : MonoBehaviour
{
    [SerializeField] private TextMesh label;

    private GuardBotAI guard;
    private BruteAI brute;

    private void Awake()
    {
        guard = GetComponentInParent<GuardBotAI>();
        brute = GetComponentInParent<BruteAI>();
    }

    private void LateUpdate()
    {
        if (label == null)
            return;

        bool show = DebugOverlay.Enabled;
        MeshRenderer meshRenderer = label.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = show;
        if (!show)
            return;

        if (guard != null)
        {
            label.text = guard.CurrentStateName;
            label.color = guard.HasLineOfSight ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.4f);
        }
        else if (brute != null)
        {
            label.text = brute.CurrentStateName;
            label.color = new Color(1f, 0.6f, 0.5f);
        }
        // Keep the text upright and unflipped regardless of the body flip.
        label.transform.rotation = Quaternion.identity;
    }
}
