using UnityEngine;

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

public struct DamageInfo
{
    public int amount;
    public Vector2 knockback;
    public bool fromRecall;
    public GameObject source;
}
