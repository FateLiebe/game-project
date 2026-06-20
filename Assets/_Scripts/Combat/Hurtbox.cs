using UnityEngine;

/// <summary>
/// Vùng nhận sát thương (Hurtbox). Gắn trên Player hoặc Quái.
/// Là điểm tiếp xúc đầu tiên nhận gói sát thương (DamageInfo) từ Hitbox. Xử lý cơ chế Né Hoàn Hảo (Perfect Dodge) trước khi trừ máu thực sự.
/// </summary>
public class Hurtbox : MonoBehaviour, IDamageable
{
    [SerializeField] private BaseEntity owner;
    public bool isPerfectDodging = false;

    public void TakeDamage(DamageInfo info)
    {
        if (isPerfectDodging)
        {
            // [FIX #2]: KHÔNG TẮT isPerfectDodging Ở ĐÂY NỮA
            // Để Coroutine PerfectDodgeWindowActive() tự động tắt sau 0.4s

            PlayerController pc = owner as PlayerController;
            if (pc != null)
            {
                BaseEntity attackerEntity = null;
                if (info.attacker != null)
                {
                    attackerEntity = info.attacker.GetComponent<BaseEntity>();
                }
                
                pc.OnPerfectDodgeSuccess(attackerEntity); 
            }
            return; 
        }

        if (owner != null) 
        {
            owner.ApplyDamage(info);
        }
    }
}