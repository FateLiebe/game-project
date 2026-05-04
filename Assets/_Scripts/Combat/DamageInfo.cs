using UnityEngine;

// Phân loại đòn đánh để xử lý Counter hoặc Kháng phép
public enum DamageType { Normal, Heavy, Skill, Pierce, Environmental }

public struct DamageInfo
{
    public float damage;
    public float poiseDamage; // Sát thương phá thế (để gây choáng/stagger)
    public Vector2 knockbackForce; // Lực đẩy lùi chuẩn xác của đòn đó
    public GameObject attacker; // Kẻ ra đòn (để Player biết quay mặt lại Counter)
    public DamageType type;
}