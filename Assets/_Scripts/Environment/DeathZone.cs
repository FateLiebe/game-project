using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Xử lý các Entity (Player, Boss, Enemy)
        BaseEntity entity = collision.GetComponentInParent<BaseEntity>();
        if (entity != null && entity.currentHealth > 0)
        {
            // Gây sát thương chuẩn khổng lồ để chắc chắn hạ gục ngay lập tức
            DamageInfo instantDeath = new DamageInfo
            {
                damage = 999999f,
                knockbackForce = Vector2.zero,
                attacker = this.gameObject
            };
            entity.ApplyDamage(instantDeath);

            // Tắt trọng lực để xác không bị rơi mãi mãi (tuỳ chọn)
            Rigidbody2D rb = entity.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        // Dọn dẹp rác, vật phẩm rơi vãi hoặc đạn bay ra khỏi map để chống rò rỉ bộ nhớ
        if (collision.CompareTag("ItemDrop") || collision.GetComponent<Projectile>() != null)
        {
            PooledObject pooledObj = collision.GetComponent<PooledObject>();
            if (pooledObj != null)
            {
                pooledObj.ReturnToPool();
            }
            else
            {
                Destroy(collision.gameObject);
            }
        }
    }
}
