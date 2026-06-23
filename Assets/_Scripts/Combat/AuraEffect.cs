using UnityEngine;

/// <summary>
/// Hiệu ứng hào quang/Bùa lợi (Buff/Shield).
/// Bám theo Thực thể (Owner) và cường hóa chỉ số (Tấn công, Phòng thủ, Khiên chắn) trong một khoảng thời gian nhất định rồi tự tiêu biến.
/// </summary>
public class AuraEffect : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    private BaseEntity ownerEntity;
    private float activeAttackBuff;
    private float activeDefenseBuff;
    #endregion

    #region PUBLIC METHODS
    // Boss hoặc Player sẽ gọi hàm này và truyền chỉ số cụ thể vào
    /// <summary>
    /// Khởi tạo và thiết lập các chỉ số buff cho Aura.
    /// Tự động cộng các chỉ số tương ứng vào chủ thể.
    /// </summary>
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
    #endregion

    #region UNITY LIFECYCLE
    /// <summary>
    /// Theo dõi và đếm ngược thời gian duy trì Aura.
    /// Có hỗ trợ ảnh hưởng bởi tính năng ngưng đọng thời gian (timeMultiplier).
    /// </summary>
    private void Update()
    {
        if (ownerEntity == null)
        {
            ReturnOrDestroy();
        }
    }

    /// <summary>
    /// Đảm bảo buff được xóa sạch kể cả khi GameObject bị vô hiệu hóa đột ngột.
    /// </summary>
    private void OnDisable()
    {
        ownerEntity = null;
    }
    #endregion

    #region PRIVATE METHODS
    /// <summary>
    /// Hủy bỏ hiệu ứng buff khỏi chủ thể và dọn dẹp các trạng thái.
    /// </summary>
    private void RemoveAura()
    {
        if (ownerEntity != null)
        {
            // Trả lại chỉ số ban đầu. Khiên nếu còn dư cũng sẽ tự biến mất.
            ownerEntity.buffAttack -= activeAttackBuff;
            ownerEntity.buffDefense -= activeDefenseBuff;
            ownerEntity.currentShield = 0;
        }
        ReturnOrDestroy();
    }

    /// <summary>
    /// Hủy GameObject hoặc trả về Object Pool để tái sử dụng.
    /// </summary>
    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
    #endregion
}