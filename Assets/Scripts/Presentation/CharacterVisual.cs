using UnityEngine;

// Cheap life for single-frame pixel sprites: a 1-pixel walk bob, facing flip, and status tints.
// Lives on the character root; only moves the child body transform so physics is untouched.
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private bool flipFromVelocity = true;
    [SerializeField] private float bobRate = 10f;
    [SerializeField] private Color rootedTint = new Color(0.55f, 0.75f, 1f);
    [SerializeField] private Color windupTint = new Color(1f, 0.55f, 0.45f);

    private const float Pixel = 1f / 32f;
    private Rigidbody2D rb;
    private EnemyBase enemy;
    private BruteAI brute;
    private Vector3 baseLocal;
    private float bobPhase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemy = GetComponent<EnemyBase>();
        brute = GetComponent<BruteAI>();
        if (body != null)
            baseLocal = body.localPosition;
    }

    private void LateUpdate()
    {
        if (body == null)
            return;

        Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;
        bool moving = velocity.sqrMagnitude > 0.25f && Time.timeScale > 0f;
        if (moving)
            bobPhase += Time.deltaTime * bobRate;
        else
            bobPhase = 0f;

        Vector3 offset = Vector3.zero;
        if (moving)
            offset.y = Mathf.Sin(bobPhase) > 0f ? Pixel : 0f;

        bool windingUp = brute != null && brute.CurrentStateName == "WindUp";
        if (windingUp)
            offset.x = (Mathf.FloorToInt(Time.time * 30f) % 2 == 0 ? 1f : -1f) * Pixel;

        body.localPosition = baseLocal + offset;

        if (bodySprite == null)
            return;
        if (flipFromVelocity && enemy != null && Mathf.Abs(velocity.x) > 0.1f)
            bodySprite.flipX = velocity.x < 0f;
        if (enemy != null && Time.timeScale > 0f)
        {
            // Health's blink only touches alpha, so keep its alpha and swap the RGB tint.
            Color tint = enemy.IsRooted ? rootedTint : windingUp ? windupTint : Color.white;
            tint.a = bodySprite.color.a;
            bodySprite.color = tint;
        }
    }
}
