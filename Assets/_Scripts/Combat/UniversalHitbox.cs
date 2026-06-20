using UnityEngine;

/// <summary>
/// Vùng gây sát thương (Hitbox) đa dụng gắn trên Vũ khí hoặc Đạn.
/// Chịu trách nhiệm phát hiện va chạm với Hurtbox, lọc chống "tự sát thương" (Friendly Fire) và truyền DamageInfo cho nạn nhân.
/// </summary>
public class UniversalHitbox : MonoBehaviour 
{
    [Header("Damage Settings")]
    public Vector2 baseKnockback = new Vector2(5f, 2f);
    
    public float damageOverride = 0f; 
    public bool isCriticalOverride = false; // [MỚI THÊM]: Cờ lưu trạng thái bạo kích của đạn

    [Header("Hitbox Owner")]
    public GameObject owner;

    /// <summary>
    /// Xử lý va chạm và truyền sát thương. Lọc cẩn thận để quái không đánh trúng quái, Player không đánh trúng Player.
    /// Tính toán cả hệ số đòn đánh chí mạng (Crit) và chiều đẩy lùi (Knockback).
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Hurtbox targetHurtbox = collision.GetComponent<Hurtbox>();
        
        if (targetHurtbox != null)
        {
            // BỘ LỌC CHỐNG TỰ SÁT THƯƠNG (FRIENDLY FIRE)
            if (owner != null)
            {
                bool ownerIsPlayer = owner.CompareTag("Player") || owner.transform.root.CompareTag("Player");
                bool targetIsPlayer = collision.gameObject.CompareTag("Player") || collision.transform.root.CompareTag("Player");

                if (ownerIsPlayer && targetIsPlayer) return;
                if (!ownerIsPlayer && !targetIsPlayer) return;
            }

            Vector2 originPos = owner != null ? (Vector2)owner.transform.position : (Vector2)transform.position;
            int pushDirection = targetHurtbox.transform.position.x < originPos.x ? -1 : 1;
            Vector2 finalKnockback = new Vector2(baseKnockback.x * pushDirection, baseKnockback.y);

            float finalDamage = 0f;
            bool isCriticalHit = false;

            if (damageOverride > 0f)
            {
                finalDamage = damageOverride;
                isCriticalHit = isCriticalOverride; // [ĐÃ SỬA]: Ép cờ bạo kích từ Player vào
            }
            else if (owner != null)
            {
                PlayerController player = owner.GetComponent<PlayerController>();
                if (player != null)
                {
                    finalDamage = player.GetCurrentMeleeDamage(out isCriticalHit);
                }
                else
                {
                    BaseEntity enemy = owner.GetComponent<BaseEntity>();
                    if (enemy != null)
                    {
                        BossHitboxData bossData = GetComponent<BossHitboxData>();
                        if (bossData != null) finalDamage = bossData.CalculateDamage(enemy.Attack, transform.position);
                        else finalDamage = enemy.Attack;
                    }
                }
            }
            else 
            {
                finalDamage = 10f; 
            }

            DamageInfo info = new DamageInfo
            {
                damage = finalDamage,
                knockbackForce = finalKnockback,
                attacker = this.owner,
                sourceHitbox = this.gameObject, // Truyền trực tiếp GameObject Hitbox vào
                isCritical = isCriticalHit 
            };

            targetHurtbox.TakeDamage(info);

            // [LOCK TARGET] Làm mới timer nếu skill của Player chạm trúng locked target
            if (owner != null && owner.CompareTag("Player"))
            {
                PlayerController pc = owner.GetComponent<PlayerController>();
                if (pc != null)
                {
                    // Kiểm tra xem target bị hit có phải là locked target không
                    BaseEntity hitEntity = targetHurtbox.GetComponentInParent<BaseEntity>();
                    if (hitEntity != null) pc.RefreshLockTimerIfMatch(hitEntity.transform);
                }
            }
        }
    }
}