using UnityEngine;

public class UniversalHitbox : MonoBehaviour 
{
    [Header("Damage Settings")]
    public Vector2 baseKnockback = new Vector2(5f, 2f);
    
    [Header("Hitbox Owner")]
    [Tooltip("Kéo cha của Hitbox (Player hoặc Enemy) vào đây")]
    public GameObject owner;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Hurtbox targetHurtbox = collision.GetComponent<Hurtbox>();
        
        if (targetHurtbox != null)
        {
            // ==========================================
            // --- BỘ LỌC MỤC TIÊU (CHẶN QUÁI ĐÁNH LUNG TUNG) ---
            // ==========================================
            if (owner != null && owner.CompareTag("Enemy"))
            {
                // Kiểm tra xem nạn nhân có mang tag "Player" không (quét bản thân nó và đối tượng cha)
                bool isHittingPlayer = collision.gameObject.CompareTag("Player") || collision.transform.root.CompareTag("Player");
                
                // Nếu nạn nhân KHÔNG PHẢI Player (mà là Thùng, Cây, v.v...) -> Hủy đòn đánh!
                if (!isHittingPlayer) return; 
            }
            // ==========================================

            int pushDirection = targetHurtbox.transform.position.x < owner.transform.position.x ? -1 : 1;
            Vector2 finalKnockback = new Vector2(baseKnockback.x * pushDirection, baseKnockback.y);

            // --- TÍNH TOÁN SÁT THƯƠNG ĐỘNG TỪ CHỈ SỐ ---
            float finalDamage = 0f;
            bool isCriticalHit = false;

            if (owner != null)
            {
                PlayerController player = owner.GetComponent<PlayerController>();
                if (player != null)
                {
                    // Nếu chủ nhân là Player -> Lấy Sát thương có tính Combo và Crit
                    finalDamage = player.GetCurrentMeleeDamage(out isCriticalHit);
                }
                else
                {
                    BaseEntity enemy = owner.GetComponent<BaseEntity>();
                    if (enemy != null)
                    {
                        // Nếu chủ nhân là Quái -> Lấy thẳng chỉ số Attack (không Crit, không Combo)
                        finalDamage = enemy.Attack;
                    }
                }
            }
            else 
            {
                // Fallback (phòng hờ bạn tạo bẫy chông môi trường không có chủ)
                finalDamage = 10f; 
            }

            DamageInfo info = new DamageInfo
            {
                damage = finalDamage,
                knockbackForce = finalKnockback,
                attacker = this.owner,
                isCritical = isCriticalHit // Gắn cờ Crit vào Info
            };

            targetHurtbox.TakeDamage(info);
        }
    }
}