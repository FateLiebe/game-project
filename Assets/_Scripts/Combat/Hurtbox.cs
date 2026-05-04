using UnityEngine;

// CHỈ CÓ FILE NÀY mới có ", IDamageable"
public class Hurtbox : MonoBehaviour, IDamageable
{
    [SerializeField] private BaseEntity owner;
    public bool isPerfectDodging = false;

    public void TakeDamage(DamageInfo info)
    {
        // TẦNG 2: XÁC NHẬN VA CHẠM THÀNH CÔNG (Confirm Success)
        if (isPerfectDodging)
        {
            isPerfectDodging = false; // Tắt ngay lập tức để không kích đúp chiêu

            PlayerController pc = owner as PlayerController;
            if (pc != null)
            {
                // FIX LỖI CS1503: Lấy Component BaseEntity từ GameObject
                BaseEntity attackerEntity = null;
                if (info.attacker != null)
                {
                    attackerEntity = info.attacker.GetComponent<BaseEntity>();
                }
                
                pc.OnPerfectDodgeSuccess(attackerEntity); 
            }
            return; 
        }

        // Nếu không lướt hoặc trượt Perfect Dodge -> Ăn đòn
        if (owner != null) 
        {
            owner.ApplyDamage(info);
        }
    }
}