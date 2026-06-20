using UnityEngine;

/// <summary>
/// Chứa dữ liệu động gắn trên từng loại VFX của Boss.
/// Cho phép tính toán sát thương uyển chuyển dựa trên Loại Kỹ Năng và Khoảng Cách bay của đạn (Ví dụ: Cầu lửa FireBall bay càng xa nổ càng đau).
/// </summary>
public class BossHitboxData : MonoBehaviour
{
    public enum BossSkillType 
    { 
        Breath, 
        BreathFire, 
        SlashHorizontal, 
        FireBall, 
        ElectroShock 
    }

    [Header("Loại skill — set trong prefab")]
    public BossSkillType skillType;

    [Header("FireBall only")]
    [Tooltip("Phải khớp với skillRanges[5] trong BossController")]
    public float fireBallMaxRange = 15f;

    // Set tự động bởi BossSkillManager khi spawn VFX
    [HideInInspector] public Vector3 spawnPosition;

    public float CalculateDamage(float bossAttack, Vector3 currentPosition)
    {
        switch (skillType)
        {
            case BossSkillType.Breath:
                return bossAttack * Random.Range(0.75f, 0.85f);

            case BossSkillType.BreathFire:
                return bossAttack * Random.Range(0.90f, 0.95f);

            case BossSkillType.SlashHorizontal:
                return bossAttack * Random.Range(1.00f, 1.05f);

            case BossSkillType.ElectroShock:
                return bossAttack * Random.Range(0.80f, 0.90f);

            case BossSkillType.FireBall:
                float dist = Vector3.Distance(spawnPosition, currentPosition);
                float t = Mathf.Clamp01(dist / fireBallMaxRange);
                float multiplier = Mathf.Lerp(1.00f, 1.50f, t);
                return bossAttack * multiplier;

            default:
                return bossAttack;
        }
    }
}