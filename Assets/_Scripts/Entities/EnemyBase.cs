using UnityEngine;

public class EnemyBase : BaseEntity 
{
    public enum EnemyRank
    {
        Normal,
        Elite,
        Boss
    }

    [Header("--- ENEMY CONFIG ---")]
    public EnemyRank rank = EnemyRank.Normal; // [ĐÃ SỬA]: Thành public để AI đọc được phân loại

    [Header("EXP Reward")]
    [SerializeField] protected float expMultiplier = 10f;

    // ==========================================
    // HỆ THỐNG COOLDOWN KỸ NĂNG
    // ==========================================
    [Header("--- ATTACK COOLDOWNS ---")]
    [Tooltip("Khai báo CD cho từng chiêu. Normal 1 chiêu, Elite 2 chiêu...")]
    public float[] attackCooldowns = new float[] { 3f }; // Mặc định có 1 đòn, CD 3 giây
    
    protected float[] currentAttackCooldowns;

    protected SpriteRenderer sr; // [ĐÃ SỬA]: Thành protected
    protected Rigidbody2D rb;

    private float baseEnemyHP = 150f;
    private float baseEnemyATK = 12f;
    private float baseEnemyDEF = 3f;

    // [MỚI]: Biến lưu màu gốc (Dùng để duy trì màu đỏ cho Elite)
    protected Color defaultColor = Color.white;

    public override float MaxHealth => baseEnemyHP + ((currentLevel - 1) * 30f);
    public override float Attack => baseEnemyATK + ((currentLevel - 1) * 4f);
    public override float Defense => baseEnemyDEF + ((currentLevel - 1) * 1f);

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // [MỚI]: Nhận diện Elite và tô màu đỏ nhạt mặc định
        if (rank == EnemyRank.Elite) 
        {
            defaultColor = new Color(1f, 0.5f, 0.5f); // Đỏ nhạt
            if (sr != null) sr.color = defaultColor;
        }

        // Khởi tạo độ dài mảng theo dõi CD bằng đúng với số lượng đòn đánh
        currentAttackCooldowns = new float[attackCooldowns.Length];
    }

    protected virtual void Update()
    {
        if (currentAttackCooldowns != null)
        {
            for (int i = 0; i < currentAttackCooldowns.Length; i++)
            {
                if (currentAttackCooldowns[i] > 0)
                {
                    // Nhân với timeMultiplier để đóng băng đếm lùi khi bị Time Stop
                    currentAttackCooldowns[i] -= Time.deltaTime * timeMultiplier;
                }
            }
        }
    }

    protected override void InitializeStats()
    {
        currentHealth = MaxHealth;
        base.InitializeStats(); 
    }
    
    public bool CanAttack(int attackIndex)
    {
        // Chống lỗi văng game nếu AI gọi một chiêu không tồn tại
        if (attackIndex < 0 || attackIndex >= attackCooldowns.Length) return false;

        return currentAttackCooldowns[attackIndex] <= 0f;
    }

    public void RecordAttackUsage(int attackIndex)
    {
        if (attackIndex >= 0 && attackIndex < attackCooldowns.Length)
        {
            currentAttackCooldowns[attackIndex] = attackCooldowns[attackIndex];
        }
    }

    public float GetExpReward()
    {
        float baseExp = currentLevel * expMultiplier;

        switch(rank)
        {
            case EnemyRank.Elite:
                baseExp *= 2f;
                break;
            case EnemyRank.Boss:
                baseExp *= 5f;
                break;
        }

        float randomFactor = Random.Range(0.85f, 1.15f);
        float finalExp = Mathf.Round(baseExp * randomFactor * 1000f) / 1000f;

        if (Random.value < 0.05f)
        {
            finalExp *= 2f;
            Debug.Log("<color=yellow>JACKPOT! X2 EXP TỪ QUÁI VẬT!</color>");
        }

        return finalExp;
    }

    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info); 
        
        if (isDead || currentHealth <= 0) 
        {
            CancelInvoke(nameof(ResetColor));
            return;
        }

        if (sr != null)
        {
            sr.color = Color.red; // Nháy đỏ rực khi bị chém
            Invoke(nameof(ResetColor), 0.1f);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(info.knockbackForce, ForceMode2D.Impulse);
        }
    }

    private void ResetColor() 
    { 
        // [ĐÃ SỬA]: Trả về defaultColor thay vì Color.white
        if (sr != null) sr.color = defaultColor; 
    }

    [Header("Combat References")]
    [SerializeField] protected GameObject attackHitbox;
    [SerializeField] protected GameObject rangedAttackHitbox;
    
    public void EnableHitbox() 
    { 
        if (attackHitbox != null) 
        {
            attackHitbox.SetActive(true); 
            // [BẢO HIỂM]: Ép buộc Hitbox (và VFX chém) TỰ ĐỘNG TẮT sau 0.5 giây.
            // Điều này đảm bảo VFX sẽ được reset và đánh lại ở lần tiếp theo!
            CancelInvoke(nameof(DisableHitbox));
            Invoke(nameof(DisableHitbox), 0.5f); 
        }
    }
    
    public void DisableHitbox() 
    { 
        if (attackHitbox != null) attackHitbox.SetActive(false); 
    }
}