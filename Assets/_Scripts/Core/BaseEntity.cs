using UnityEngine;
using System; 

public class BaseEntity : MonoBehaviour
{
    [Header("Data Configuration")]
    public CharacterDataSO baseData;

    [Header("--- UI SETTINGS ---")]
    public GameObject damagePopupPrefab; // Nơi nhét Prefab vừa tạo

    protected float currentMoveSpeed;
    protected float currentHealth;

    // Đồng hồ thời gian cục bộ của sinh vật này
    [HideInInspector] public float timeMultiplier = 1f;

    // Sự kiện phát thanh báo cho UI biết mỗi khi máu thay đổi
    public event Action<float, float> OnHealthChanged;
    
    // [MỚI THÊM]: Kênh phát thanh cho Level và EXP
    public event Action<int> OnLevelChanged;
    public event Action<float, float> OnExpChanged;

    [Header("--- RPG PROGRESSION ---")]
    public int currentLevel = 1;
    public int currentStatPoints = 0; 
    
    [Header("--- EXP SYSTEM ---")]
    public float currentEXP = 0f;
    public float expToNextLevel = 30f;

    [Header("--- ALLOCATED POINTS (Điểm đã cộng) ---")]
    public int addedHealthPoints = 0;
    public int addedAttackPoints = 0;
    public int addedDefensePoints = 0;
    public int addedCritPoints = 0;

    // Các chỉ số linh động, tự động tính toán từ Data gốc + Cấp độ + Điểm Tiềm Năng
    // Công thức: 1 điểm = 10 Máu, 5 Công, 2 Thủ, 1% Crit
    public float MaxHealth => baseData.baseMaxHealth + ((currentLevel - 1) * baseData.healthGrowth) + (addedHealthPoints * 10f);
    public float Attack => baseData.baseAttack + ((currentLevel - 1) * baseData.attackGrowth) + (addedAttackPoints * 5f);
    public float Defense => baseData.baseDefense + ((currentLevel - 1) * baseData.defenseGrowth) + (addedDefensePoints * 2f);
    public float CritRate => baseData.baseCritRate + ((currentLevel - 1) * baseData.critRateGrowth) + (addedCritPoints * 1f);
    
    // Tốc độ cố định, không tăng theo cấp. (Sau này nếu mặc giày, ta chỉ cần sửa thành: moveSpeed + gearBonusSpeed)
    public float Speed => baseData.moveSpeed;

    protected virtual void Start()
    {
        InitializeStats();
    }

    public virtual void ApplyDamage(DamageInfo info)
    {
        float finalDamage = Mathf.Max(1f, info.damage - Defense);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        
        // ---- HIỂN THỊ POPUP SÁT THƯƠNG ----
        if (damagePopupPrefab != null)
        {
            // Bắn popup ra ngay trên đầu nhân vật 1 chút
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.Setup(finalDamage, info.isCritical);
        }

        if (currentHealth <= 0)
        {
            if (info.attacker != null)
            {
                BaseEntity attackerEntity = info.attacker.GetComponent<BaseEntity>();
                if (attackerEntity != null && info.attacker.CompareTag("Player"))
                {
                    attackerEntity.GainEXP(finalDamage * 1.5f); 
                }
            }
            Die();
        }
    }

    protected virtual void Die()
    {
        gameObject.SetActive(false);
        Debug.Log(gameObject.name + " đã ngỏm!");
    }

    protected virtual void InitializeStats()
    {
        if (baseData != null)
        {
            currentMoveSpeed = Speed; 
            currentHealth = MaxHealth; 
            
            // Báo cho UI cập nhật lần đầu tiên khi vừa chạy game
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            OnLevelChanged?.Invoke(currentLevel);
            OnExpChanged?.Invoke(currentEXP, expToNextLevel);
        }
    }

    public void GainEXP(float amount)
    {
        currentEXP += amount;
        
        // Hét lên cho thanh EXP biết để chạy hiệu ứng
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);
        
        Debug.Log($"<color=cyan>Hấp thụ {amount} EXP!</color> (Tiến trình: {currentEXP}/{expToNextLevel})");

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
        
        // Cập nhật toàn bộ UI sau khi lên cấp
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnLevelChanged?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);

        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupText("LEVEL UP!", Color.green);
        }
    }

    // --- HÀM MỚI: Xử lý khi người chơi bấm nút (+) trên giao diện ---
    public void AllocateStatPoint(string statType)
    {
        if (currentStatPoints <= 0) return; // Hết điểm thì nghỉ
        
        currentStatPoints--; // Trừ 1 điểm tiềm năng
        
        switch (statType)
        {
            case "HP":
                addedHealthPoints++;
                currentHealth += 10f; // Cộng vào máu tối đa thì bơm luôn 10 máu hiện tại cho khỏi thiệt
                break;
            case "ATK":
                addedAttackPoints++;
                break;
            case "DEF":
                addedDefensePoints++;
                break;
            case "CRIT":
                addedCritPoints++;
                break;
        }
        
        // Hét lên cho thanh máu trên đầu nhân vật cập nhật giới hạn máu mới
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }
}