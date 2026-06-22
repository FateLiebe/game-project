using UnityEngine;

[CreateAssetMenu(fileName = "NewBossData", menuName = "Data/Boss Data", order = 3)]
public class BossDataSO : EnemyDataSO
{
    [Header("Boss Specific Multipliers")]
    public float bossHealthMultiplier = 8f;
    public float bossAttackBuffMultiplier = 0.8f;   // Dùng để buff trực tiếp trong Start()
    public float bossDefenseBuffMultiplier = 1.0f;  // Dùng để buff trực tiếp trong Start()
    
    [Header("Smack Buff Multipliers")]
    public float smackAttackMultiplier = 0.5f;
    public float smackDefenseMultiplier = 0.7f;
}
