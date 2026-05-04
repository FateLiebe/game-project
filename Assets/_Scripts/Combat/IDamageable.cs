using UnityEngine;

// Giao diện này dùng cho bất cứ thứ gì có thể bị đánh mất máu
public interface IDamageable
{
    // Bắt buộc phải có hàm nhận sát thương
    void TakeDamage(DamageInfo info);
}