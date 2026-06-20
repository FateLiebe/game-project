using UnityEngine;

/// <summary>
/// Thẻ Dữ Liệu Tĩnh (ScriptableObject) lưu trữ toàn bộ cấu hình gốc của Nhân vật.
/// Bao gồm thông số Di chuyển (Tốc độ, Bước nhảy, Lướt) và Hệ số Tăng trưởng (Growth) khi lên cấp.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Data/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Movement & Jump Stats")]
    public float moveSpeed = 8f;
    public float jumpForce = 12.5f;
    public float fastFallSpeed = 18f;
    public int maxJumps = 2;

    [Header("Dash Stats")]
    public float dashForce = 24f;
    public float dashTime = 0.18f;
    public float dashStallTime = 0.22f; 
    public float dashRechargeTime = 1.5f;
    public int maxDashes = 2;
    public int maxAirDashes = 2;

    [Header("Combat & Input Settings")]
    public float comboResetTime = 0.6f;
    public float doubleTapThreshold = 0.25f;

    [Header("--- RPG BASE STATS (Lv 1) ---")]
    public float baseMaxHealth = 30f;
    public float baseAttack = 10f;
    public float baseDefense = 4f;
    public float baseCritRate = 5f; // 5 tương đương 5%
    public float critDamageMultiplier = 1.7f; // Sát thương x1.7 khi bạo kích

    [Header("--- RPG LEVEL UP GROWTH ---")]
    public float healthGrowth = 10f;
    public float attackGrowth = 5f;
    public float defenseGrowth = 3f;
    public float critRateGrowth = 0.5f;

    [Header("--- RPG PROGRESSION ---")]
    public int statPointsPerLevel = 3; // [ĐÃ CHUẨN HOÁ]: Mỗi cấp được 3 điểm
}