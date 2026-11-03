using System;
using UnityEngine;

// F2 toggles in-game debug visuals for the video: spawn point eligibility and enemy AI state labels.
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [SerializeField] private float minimumSpawnDistance = 8f;
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Color eligibleColor = new Color(0.3f, 1f, 0.4f, 0.8f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    private static bool overlayEnabled;
    public static event Action<bool> EnabledChanged;

    public static bool Enabled
    {
        get => overlayEnabled;
        set
        {
            if (overlayEnabled == value) return;
            overlayEnabled = value;
            EnabledChanged?.Invoke(value);
        }
    }

    private SpriteRenderer[] markers = new SpriteRenderer[0];

    private void Awake()
    {
        markers = new SpriteRenderer[spawnPoints.Length];
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
                continue;
            GameObject marker = new GameObject("Spawn Marker");
            marker.transform.SetParent(spawnPoints[i], false);
            markers[i] = marker.AddComponent<SpriteRenderer>();
            markers[i].sprite = markerSprite;
            markers[i].sortingOrder = 50;
            markers[i].enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            Enabled = !Enabled;

        PlayerController player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
                continue;
            markers[i].enabled = Enabled;
            if (!Enabled || player == null)
                continue;
            float distance = Vector2.Distance(spawnPoints[i].position, player.transform.position);
            markers[i].color = distance >= minimumSpawnDistance ? eligibleColor : blockedColor;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        overlayEnabled = false;
        EnabledChanged = null;
    }
}
