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
    public GameObject sourceHitbox; // Lưu vết Hitbox gốc sinh ra sát thương để nhận diện Phản đòn (Parry/Counter)
    public bool isCritical;         // Cờ báo hiệu đòn đánh này có phải là chí mạng hay không
}