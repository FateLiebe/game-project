using UnityEngine;
using System; 

public class BaseEntity : MonoBehaviour
{
    [Header("Data Configuration")]
    public CharacterDataSO baseData;

    [Header("--- UI SETTINGS ---")]
    public GameObject damagePopupPrefab; // Nơi nhét Prefab vừa tạo

    protected float currentMoveSpeed;
    public float currentHealth;

    // Đồng hồ thời gian cục bộ của sinh vật này
    [HideInInspector] public float timeMultiplier = 1f;

    // Sự kiện phát thanh báo cho UI biết mỗi khi máu thay đổi
    public event Action<float, float> OnHealthChanged;
    
    // Kênh phát thanh cho Level và EXP
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
    // Nếu thời gian hiện tại trừ đi lúc đánh nhau gần nhất mà nhỏ hơn 5 giây -> Đang trong combat
    public bool IsInCombat => Time.time - lastCombatTime < 5f;

    [Header("--- EQUIPMENT BONUS (Từ Đồ Mặc) ---")]
    public float equipHealthBonus = 0f;
    public float equipAttackBonus = 0f;
    public float equipDefenseBonus = 0f;
    public float equipCritRateBonus = 0f;
    public float equipCritDamageBonus = 0f;
    public float equipSpeedBonus = 0f; // [MỚI THÊM]: Bonus Tốc độ

    [Header("--- ALLOCATED POINTS (Điểm đã cộng) ---")]
    public int addedHealthPoints = 0;
    public int addedAttackPoints = 0;
    public int addedDefensePoints = 0;
    public int addedCritPoints = 0;

    // Các chỉ số linh động, tự động tính toán từ Data gốc + Cấp độ + Điểm Tiềm Năng
    public float MaxHealth => baseData.baseMaxHealth + ((currentLevel - 1) * baseData.healthGrowth) + (addedHealthPoints * 10f) + equipHealthBonus;
    public float Attack => baseData.baseAttack + ((currentLevel - 1) * baseData.attackGrowth) + (addedAttackPoints * 2f) + equipAttackBonus;
    public float Defense => baseData.baseDefense + ((currentLevel - 1) * baseData.defenseGrowth) + (addedDefensePoints * 2f) + equipDefenseBonus;
    public float CritRate => baseData.baseCritRate + ((currentLevel - 1) * baseData.critRateGrowth) + (addedCritPoints * 1f) + equipCritRateBonus;
    public float CritDamage => (baseData.critDamageMultiplier * 100f) + equipCritDamageBonus;    
    
    // [ĐÃ SỬA]: Tốc độ giờ đã được cộng dồn với trang bị (Giày)
    public float Speed => baseData.moveSpeed + equipSpeedBonus;

    protected virtual void Start()
    {
        InitializeStats();
    }

    public virtual void ApplyDamage(DamageInfo info)
    {

        // Ghi nhận thời gian bị ăn đòn
        lastCombatTime = Time.time; 
        
        // Ghi nhận luôn thời gian của kẻ vừa đánh mình (để cả 2 cùng vào trạng thái combat)
        if (info.attacker != null)
        {
            BaseEntity attackerEntity = info.attacker.GetComponent<BaseEntity>();
            if (attackerEntity != null) attackerEntity.lastCombatTime = Time.time;
        }

        float finalDamage = Mathf.Max(1f, info.damage - Defense);

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        
        // ==========================================
        // [ĐÃ SỬA] ---- HIỂN THỊ POPUP SÁT THƯƠNG ----
        // ==========================================
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) 
            {
                // Kiểm tra xem nạn nhân có phải là Player không để chọn màu Đỏ/Trắng
                bool isPlayer = gameObject.CompareTag("Player");
                popupScript.SetupDamage(finalDamage, info.isCritical, isPlayer);
            }
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

        // ==========================================
        // [MỚI THÊM] HIỂN THỊ POPUP KINH NGHIỆM
        // ==========================================
        if (damagePopupPrefab != null)
        {
            // Bắn popup văng hơi lệch sang phải một chút để không đè lên sát thương
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
        
        // Cập nhật toàn bộ UI sau khi lên cấp
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnLevelChanged?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentEXP, expToNextLevel);

        // ==========================================
        // [ĐÃ SỬA] HIỂN THỊ POPUP LEVEL UP (NHẤP NHÁY)
        // ==========================================
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null) popupScript.SetupLevelUp();
        }
    }

    // --- HÀM MỚI: Xử lý khi người chơi bấm nút (+) trên giao diện ---
    public void AllocateStatPoint(string statType)
    {
        if (currentStatPoints <= 0) return; 
        
        currentStatPoints--; 
        
        switch (statType)
        {
            case "HP":
                addedHealthPoints++;
                currentHealth += 10f; 
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

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth); 
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth); 
        Debug.Log($"<color=green>Hồi phục {amount} máu! ({currentHealth}/{MaxHealth})</color>");
    }

    // [ĐÃ SỬA]: Thêm tham số critDmg và speedBonus (đặt mặc định = 0f để không lỗi code cũ)
    public void UpdateEquipmentStats(float hp, float atk, float def, float crit, float critDmg = 0f, float speedBonus = 0f)
    {
        equipHealthBonus = hp;
        equipAttackBonus = atk;
        equipDefenseBonus = def;
        equipCritRateBonus = crit;
        equipCritDamageBonus = critDmg;   // Cập nhật Sát thương bạo kích
        equipSpeedBonus = speedBonus;     // Cập nhật Tốc độ

        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        
        Debug.Log($"<color=orange>Chỉ số mới -> ATK: {Attack} | DEF: {Defense} | CRIT: {CritRate}% | CRIT DMG: {CritDamage}% | TỐC ĐỘ: {Speed}</color>");
    }
}