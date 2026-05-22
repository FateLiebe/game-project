using UnityEngine;

// Phân loại đòn đánh để xử lý Counter hoặc Kháng phép
public enum DamageType { Normal, Heavy, Skill, Pierce, Environmental }

public struct DamageInfo // (Hoặc public class tùy code cũ của bạn)
{
    public float damage;
    public Vector2 knockbackForce;
    public GameObject attacker;
    public bool isCritical; // [MỚI THÊM]: Cờ báo hiệu đòn đánh này có chí mạng không
}