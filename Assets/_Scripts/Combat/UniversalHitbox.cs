using UnityEngine;

public class UniversalHitbox : MonoBehaviour 
{
    [Header("Damage Settings")]
    public float damage = 10f;
    public Vector2 baseKnockback = new Vector2(5f, 2f);
    
    [Header("Hitbox Owner")]
    [Tooltip("Kéo cha của Hitbox (Player hoặc Enemy) vào đây")]
    public GameObject owner;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Hurtbox targetHurtbox = collision.GetComponent<Hurtbox>();
        
        if (targetHurtbox != null)
        {
            // Tự động tính toán hướng văng dựa trên vị trí Owner
            int pushDirection = targetHurtbox.transform.position.x < owner.transform.position.x ? -1 : 1;
            Vector2 finalKnockback = new Vector2(baseKnockback.x * pushDirection, baseKnockback.y);

            DamageInfo info = new DamageInfo
            {
                damage = this.damage,
                knockbackForce = finalKnockback,
                attacker = this.owner
            };

            targetHurtbox.TakeDamage(info);
        }
    }
}