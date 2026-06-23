using UnityEngine;
using System; 

/// <summary>
/// Class nền tảng (Base) cho mọi thực thể sống trong game (Player, Boss, Quái). 
/// Quản lý Máu, Level, EXP, Chỉ số cơ bản và nhận sát thương.
/// </summary>
public class BaseEntity : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("Data Configuration")]
    public CharacterDataSO baseData;

    [Header("--- UI SETTINGS ---")]
    public GameObject damagePopupPrefab; 

    protected float currentMoveSpeed;
    public float currentHealth;

    [HideInInspector] public float timeMultiplier = 1f;
    [HideInInspector] public bool isDead = false; // Cờ theo dõi trạng thái sống chết, ngăn nhận thêm sát thương khi đã gục ngã

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

    [Header("--- TEMPORARY BUFFS & SHIELD ---")]
    public float currentShield = 0f;
    public float buffAttack = 0f;
    public float buffDefense = 0f;

    public virtual float MaxHealth => baseData.baseMaxHealth + ((currentLevel - 1) * baseData.healthGrowth) + (addedHealthPoints * 5f) + equipHealthBonus;
    public virtual float Attack => baseData.baseAttack + ((currentLevel - 1) * baseData.attackGrowth) + (addedAttackPoints * 1f) + equipAttackBonus + buffAttack;
    public virtual float Defense => baseData.baseDefense + ((currentLevel - 1) * baseData.defenseGrowth) + (addedDefensePoints * 1f) + equipDefenseBonus + buffDefense;
    public float CritRate => baseData.baseCritRate + ((currentLevel - 1) * baseData.critRateGrowth) + (addedCritPoints * 0.2f) + equipCritRateBonus;
    public float CritDamage => (baseData.critDamageMultiplier * 100f) + equipCritDamageBonus;    
    public float Speed => baseData.moveSpeed + equipSpeedBonus;
    #endregion

    #region UNITY LIFECYCLE
    protected virtual void Start()
    {
        InitializeStats();
    }

    protected virtual void OnEnable()
    {
        if (baseData == null) return;
        currentHealth = MaxHealth;
        currentShield = 0;
        buffAttack = 0;
        buffDefense = 0;
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Tính toán trừ máu. Trừ đi giáp, hiển thị con số sát thương nảy lên (Damage Popup).
    /// Xử lý luôn logic chết và rơi tiền/EXP nếu đối tượng này là Quái vật.
    /// </summary>
    public virtual void ApplyDamage(DamageInfo info)
    {
        // Chặn đứng mọi lượng sát thương hoặc hiệu ứng tiếp theo nếu thực thể đã bị tiêu diệt
        if (isDead || currentHealth <= 0) return; 

        lastCombatTime = Time.time; 
        
        if (info.attacker != null)
        {
            BaseEntity attackerEntity = info.attacker.GetComponent<BaseEntity>();
            if (attackerEntity != null) attackerEntity.lastCombatTime = Time.time;
        }

        float finalDamage = Mathf.Max(1f, info.damage - Defense);

        // Logic Khiên (Shield): Hấp thụ sát thương trước khi trừ trực tiếp vào máu thật
        if (currentShield > 0)
        {
            if (currentShield >= finalDamage)
            {
                currentShield -= finalDamage;
                finalDamage = 0; // Khiên gánh hết sát thương
            }
            else
            {
                finalDamage -= currentShield; // Khiên vỡ, phần dư trừ vào máu
                currentShield = 0;
            }
        }

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        
        if (damagePopupPrefab != null)
        {
            GameObject popup;
            if (ObjectPoolManager.Instance != null) popup = ObjectPoolManager.Instance.Get(damagePopupPrefab, transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            else popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
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
                    if (enemy != null) 
                    {
                        attackerEntity.GainEXP(enemy.GetExpReward());
                        
                        // 50% rớt Coin từ quái
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            PlayerController player = attackerEntity as PlayerController;
                            if (player != null)
                            {
                                int baseCoin = 5 * enemy.currentLevel;
                                int coinReward = Mathf.RoundToInt(baseCoin * UnityEngine.Random.Range(0.9f, 1.1f));
                                player.coins += coinReward;

                                // Hiển thị popup Coin
                                if (damagePopupPrefab != null)
                                {
                                    GameObject popup;
                                    if (ObjectPoolManager.Instance != null) popup = ObjectPoolManager.Instance.Get(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
                                    else popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
                                    
                                    DamagePopup popupScript = popup.GetComponent<DamagePopup>();
                                    if (popupScript != null) popupScript.SetupCoin(coinReward);
                                }
                            }
                        }
                    }
                }
            }
            Die();
        }
    }

    public void RefreshUIAfterLoad()
    {
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnLevelChanged?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);
    }

    public void GainEXP(float amount)
    {
        currentEXP += amount;
        
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);

        if (damagePopupPrefab != null)
        {
            GameObject popup;
            if (ObjectPoolManager.Instance != null) popup = ObjectPoolManager.Instance.Get(damagePopupPrefab, transform.position + new Vector3(0.5f, 1f, 0), Quaternion.identity);
            else popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0.5f, 1f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupEXP(amount);
        }

        while (currentEXP >= expToNextLevel)
        {
            LevelUp();
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

    // Hàm này dùng để Set cấp độ trực tiếp cho Quái từ Spawner và kích hoạt UI
    public void SetLevel(int newLevel)
    {
        currentLevel = newLevel;
        currentHealth = MaxHealth; // Tự động scale máu tối đa theo level mới và bơm đầy
        
        // Kích hoạt Event để báo cho UI Canvas trên đầu quái cập nhật lại con số
        OnLevelChanged?.Invoke(currentLevel); 
        OnHealthChanged?.Invoke(currentHealth, MaxHealth); 
    }
    #endregion

    #region CORE LOGIC & PROTECTED METHODS
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

    /// <summary>
    /// Xử lý lên cấp. 
    /// Reset EXP, cộng điểm tiềm năng, bơm đầy máu và khuếch đại yêu cầu EXP cho cấp tiếp theo.
    /// </summary>
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
            GameObject popup;
            if (ObjectPoolManager.Instance != null) popup = ObjectPoolManager.Instance.Get(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
            else popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupLevelUp();
        }

        // [AUDIO] Chỉ phát âm thanh level up cho Player
        if (CompareTag("Player"))
            AudioManager.Instance?.PlayLevelUp();
    }
    #endregion
}