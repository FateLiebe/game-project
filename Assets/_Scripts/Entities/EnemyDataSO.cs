using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy Data", order = 2)]
public class EnemyDataSO : ScriptableObject
{
    #region VARIABLES & PROPERTIES
    [Header("RPG Base Stats")]
    public float baseMaxHealth = 150f;
    public float baseAttack = 12f;
    public float baseDefense = 3f;

    [Header("RPG Growth Stats")]
    public float healthGrowth = 30f;
    public float attackGrowth = 4f;
    public float defenseGrowth = 1f;

    [Header("Movement & Vision Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public float lineOfSight = 6f;
    public float attackRange = 1.8f;
    public float idleDuration = 1.5f;

    [Header("Elite / Ranged Settings")]
    public float rangedAttackRange = 6f;
    #endregion
}
