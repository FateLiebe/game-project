using UnityEngine;

/// <summary>
/// Giao diện (Interface) chung dành cho mọi vật thể có thể bị phá hủy hoặc mất máu (Quái, Người, Hòm đồ).
/// Ép buộc các class kế thừa phải định nghĩa cách xử lý TakeDamage.
/// </summary>
public interface IDamageable
{
    // Bắt buộc phải có hàm nhận sát thương
    void TakeDamage(DamageInfo info);
}