using UnityEngine;

public class AuraEffect : MonoBehaviour
{
    private BaseEntity ownerEntity;
    private float activeAttackBuff;
    private float activeDefenseBuff;

    // Boss hoặc Player sẽ gọi hàm này và truyền chỉ số cụ thể vào
    public void SetupAura(BaseEntity owner, float duration, float attackBuff, float defenseBuff, float shieldAmt)
    {
        ownerEntity = owner;
        activeAttackBuff = attackBuff;
        activeDefenseBuff = defenseBuff;

        // Cấp Buff và Khiên
        ownerEntity.buffAttack += activeAttackBuff;
        ownerEntity.buffDefense += activeDefenseBuff;
        ownerEntity.currentShield += shieldAmt;

        // Hẹn giờ thu hồi Buff và xóa hiệu ứng
        Invoke(nameof(RemoveAura), duration);
    }

    // private void Update()
    // {
    //     // Bám dính lấy chủ nhân khi di chuyển
    //     if (ownerEntity != null)
    //     {
    //         transform.position = ownerEntity.centerSpawnPoint.position;
    //     }
    //     else
    //     {
    //         Destroy(gameObject); 
    //     }
    // }

    private void Update()
    {
        if (ownerEntity == null)
        {
            Destroy(gameObject);
        }
    }

    private void RemoveAura()
    {
        if (ownerEntity != null)
        {
            // Trả lại chỉ số ban đầu. Khiên nếu còn dư cũng sẽ tự biến mất.
            ownerEntity.buffAttack -= activeAttackBuff;
            ownerEntity.buffDefense -= activeDefenseBuff;
            ownerEntity.currentShield = 0;
        }
        Destroy(gameObject);
    }
}