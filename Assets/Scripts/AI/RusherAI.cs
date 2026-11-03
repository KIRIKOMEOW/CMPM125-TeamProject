using UnityEngine;

[DisallowMultipleComponent]
public class RusherAI : EnemyBase
{
    [Header("Contact Attack")]
    [SerializeField] private int contactDamage = 10;
    [SerializeField] private float contactKnockback = 3f;

    protected override void TickMovement()
    {
        if (Target == null || Target.Health == null || Target.Health.IsDead)
        {
            Move(Vector2.zero);
            return;
        }

        Move((Vector2)Target.transform.position - Body.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryContactDamage(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryContactDamage(collision.collider);
    }

    private void TryContactDamage(Collider2D other)
    {
        if (!CanAct)
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null && other.attachedRigidbody != null)
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        if (player == null)
            return;

        IDamageable damageable = player.GetComponent<IDamageable>();
        if (damageable == null)
            return;

        Vector2 away = (Vector2)player.transform.position - Body.position;
        damageable.TakeDamage(new DamageInfo
        {
            amount = contactDamage,
            knockback = away.normalized * contactKnockback,
            fromRecall = false,
            source = gameObject
        });
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        contactDamage = Mathf.Max(1, contactDamage);
        contactKnockback = Mathf.Max(0f, contactKnockback);
    }
}
