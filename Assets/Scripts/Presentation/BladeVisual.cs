using UnityEngine;

// Spins the blade sprite while it flies and only shows the trail while it is moving.
public class BladeVisual : MonoBehaviour
{
    [SerializeField] private Transform spinner;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float spinSpeed = 1440f;

    private Blade blade;

    private void Awake()
    {
        blade = GetComponent<Blade>();
    }

    private void OnEnable()
    {
        if (trail != null)
            trail.Clear();
    }

    private void LateUpdate()
    {
        bool flying = blade != null && !blade.IsResting;
        if (spinner != null && flying)
            spinner.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);
        if (trail != null)
            trail.emitting = flying;
    }
}
