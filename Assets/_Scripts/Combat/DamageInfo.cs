using UnityEngine;

// Phân loại đòn đánh để xử lý Counter hoặc Kháng phép
public enum DamageType { Normal, Heavy, Skill, Pierce, Environmental }

/// <summary>
/// Gói Dữ Liệu Sát Thương (Struct).
/// Mang theo lượng sát thương, lực đẩy, cờ chí mạng (Crit) và vết Hitbox gốc (để truy vết và kích hoạt Phản đòn - Parry).
/// </summary>
public struct DamageInfo
{
    public float damage;
    public Vector2 knockbackForce;
    public GameObject attacker;
    public GameObject sourceHitbox; // [MỚI THÊM]: Lưu vết Hitbox để nhận diện Parry
    public bool isCritical;  // [MỚI THÊM]: Cờ báo hiệu đòn đánh này có chí mạng không
}