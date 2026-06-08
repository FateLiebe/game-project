using UnityEngine;
using System; 

public class BaseEntity : MonoBehaviour
{
    [Header("Data Configuration")]
    public CharacterDataSO baseData;

    [Header("--- UI SETTINGS ---")]
    public GameObject damagePopupPrefab; 

    protected float currentMoveSpeed;
    public float currentHealth;

    [HideInInspector] public float timeMultiplier = 1f;
    [HideInInspector] public bool isDead = false; // [FIX #4]: Cờ trạng thái sống chết

    public event Action<float, float> OnHealthChanged;
    public event Action<int> OnLevelChanged;
    public event Action<float, float> OnExpChanged;

    [Header("--- RPG PROGRESSION ---")]
    public int currentLevel = 1;
    public int currentStatPoints = 0; 
    
    [Header("--- EXP SYSTEM ---")]
    public float currentEXP = 0f;
    public float expToNextLevel = 30f;

    [Header("--- COMBAT STATE ---")]
    public float lastCombatTime = -99f;
    public bool IsInCombat => Time.time - lastCombatTime < 5f;

    [Header("--- EQUIPMENT BONUS ---")]
    public float equipHealthBonus = 0f;
    public float equipAttackBonus = 0f;
    public float equipDefenseBonus = 0f;
    public float equipCritRateBonus = 0f;
    public float equipCritDamageBonus = 0f;
    public float equipSpeedBonus = 0f; 

    [Header("--- ALLOCATED POINTS ---")]
    public int addedHealthPoints = 0;
    public int addedAttackPoints = 0;
    public int addedDefensePoints = 0;
    public int addedCritPoints = 0;

    public virtual float MaxHealth => baseData.baseMaxHealth + ((currentLevel - 1) * baseData.healthGrowth) + (addedHealthPoints * 5f) + equipHealthBonus;
    public virtual float Attack => baseData.baseAttack + ((currentLevel - 1) * baseData.attackGrowth) + (addedAttackPoints * 1f) + equipAttackBonus;
    public virtual float Defense => baseData.baseDefense + ((currentLevel - 1) * baseData.defenseGrowth) + (addedDefensePoints * 1f) + equipDefenseBonus;
    public float CritRate => baseData.baseCritRate + ((currentLevel - 1) * baseData.critRateGrowth) + (addedCritPoints * 0.2f) + equipCritRateBonus;
    public float CritDamage => (baseData.critDamageMultiplier * 100f) + equipCritDamageBonus;    
    public float Speed => baseData.moveSpeed + equipSpeedBonus;

    protected virtual void Start()
    {
        InitializeStats();
    }

    public virtual void ApplyDamage(DamageInfo info)
    {
        // [FIX #4]: Chặn đứng mọi sát thương/EXP cộng dồn nếu đã chết
        if (isDead || currentHealth <= 0) return; 

        lastCombatTime = Time.time; 
        
        if (info.attacker != null)
        {
            BaseEntity attackerEntity = info.attacker.GetComponent<BaseEntity>();
            if (attackerEntity != null) attackerEntity.lastCombatTime = Time.time;
        }

        float finalDamage = Mathf.Max(1f, info.damage - Defense);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) 
            {
                bool isPlayer = gameObject.CompareTag("Player");
                popupScript.SetupDamage(finalDamage, info.isCritical, isPlayer);
            }
        }

        if (currentHealth <= 0)
        {
            isDead = true; // Đánh dấu chết ngay lập tức

            if (info.attacker != null)
            {
                BaseEntity attackerEntity = info.attacker.GetComponent<BaseEntity>();

                if (attackerEntity != null && info.attacker.CompareTag("Player"))
                {
                    EnemyBase enemy = this as EnemyBase;
                    if (enemy != null) attackerEntity.GainEXP(enemy.GetExpReward());
                }
            }
            Die();
        }
    }

    protected virtual void Die()
    {
        // [FIX #1 & #11]: Xóa gameObject.SetActive(false);
        // Để các class con (Player/Enemy) tự quyền quyết định cách chết
    }

    protected virtual void InitializeStats()
    {
        if (baseData != null)
        {
            currentMoveSpeed = Speed; 
            currentHealth = MaxHealth; 
            
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            OnLevelChanged?.Invoke(currentLevel);
            OnExpChanged?.Invoke(currentEXP, expToNextLevel);
        }
    }

    public void GainEXP(float amount)
    {
        currentEXP += amount;
        
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);

        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0.5f, 1f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupEXP(amount);
        }

        while (currentEXP >= expToNextLevel)
        {
            LevelUp();
        }
    }

    protected virtual void LevelUp()
    {
        currentEXP -= expToNextLevel; 
        currentLevel++;
        currentStatPoints += baseData.statPointsPerLevel;
        
        float multiplier = 1.2f;
        if (currentLevel >= 10 && currentLevel < 30) multiplier = 1.3f;
        else if (currentLevel >= 30) multiplier = 1.4f; 

        expToNextLevel *= multiplier;
        expToNextLevel = Mathf.Round(expToNextLevel); 

        currentHealth = MaxHealth; 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnLevelChanged?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);

        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupLevelUp();
        }
    }

    public void AllocateStatPoint(string statType)
    {
        if (currentStatPoints <= 0) return; 
        currentStatPoints--; 
        
        switch (statType)
        {
            case "HP": addedHealthPoints++; currentHealth += 5f; break;
            case "ATK": addedAttackPoints++; break;
            case "DEF": addedDefensePoints++; break;
            case "CRIT": addedCritPoints++; break;
        }
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        OnHealthChanged?.Invoke(currentHealth, MaxHealth); 
    }

    public void UpdateEquipmentStats(float hp, float atk, float def, float crit, float critDmg = 0f, float speedBonus = 0f)
    {
        equipHealthBonus = hp;
        equipAttackBonus = atk;
        equipDefenseBonus = def;
        equipCritRateBonus = crit;
        equipCritDamageBonus = critDmg;   
        equipSpeedBonus = speedBonus;     

        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }
}