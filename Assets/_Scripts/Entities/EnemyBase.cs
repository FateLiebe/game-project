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
    [SerializeField] private EnemyRank rank = EnemyRank.Normal;

    [Header("EXP Reward")]
    [SerializeField] protected float expMultiplier = 10f;

    // ==========================================
    // HỆ THỐNG COOLDOWN KỸ NĂNG
    // ==========================================
    [Header("--- ATTACK COOLDOWNS ---")]
    [Tooltip("Khai báo CD cho từng chiêu. Normal 1 chiêu, Elite 2 chiêu...")]
    public float[] attackCooldowns = new float[] { 3f }; // Mặc định có 1 đòn, CD 3 giây
    
    // [ĐÃ SỬA]: Chuyển sang mảng đếm lùi thủ công thay vì lưu Time.time
    protected float[] currentAttackCooldowns;

    private SpriteRenderer sr;
    protected Rigidbody2D rb;

    private float baseEnemyHP = 150f;
    private float baseEnemyATK = 12f;
    private float baseEnemyDEF = 3f;

    public override float MaxHealth => baseEnemyHP + ((currentLevel - 1) * 30f);
    public override float Attack => baseEnemyATK + ((currentLevel - 1) * 4f);
    public override float Defense => baseEnemyDEF + ((currentLevel - 1) * 1f);

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // Khởi tạo độ dài mảng theo dõi CD bằng đúng với số lượng đòn đánh
        currentAttackCooldowns = new float[attackCooldowns.Length];
    }

    // [MỚI THÊM]: Hàm cập nhật đếm lùi CD chịu tác động của Time Stop
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

    // ==========================================
    // CÁC HÀM XỬ LÝ COOLDOWN
    // ==========================================
    
    // Hàm này để AI (EnemyController) gọi hỏi xem: "Đòn số mấy đã hồi xong chưa?"
    public bool CanAttack(int attackIndex)
    {
        // Chống lỗi văng game nếu AI gọi một chiêu không tồn tại
        if (attackIndex < 0 || attackIndex >= attackCooldowns.Length) return false;

        // [ĐÃ SỬA]: Được phép đánh khi đồng hồ đếm lùi đã về 0
        return currentAttackCooldowns[attackIndex] <= 0f;
    }

    // Hàm này được gọi NGAY SAU KHI quái vật vung đòn thành công (để bắt đầu đếm ngược CD)
    public void RecordAttackUsage(int attackIndex)
    {
        if (attackIndex >= 0 && attackIndex < attackCooldowns.Length)
        {
            // [ĐÃ SỬA]: Bơm lại đúng thời gian hồi chiêu gốc vào bộ đếm lùi
            currentAttackCooldowns[attackIndex] = attackCooldowns[attackIndex];
        }
    }

    // ==========================================
    // TÍNH TOÁN KINH NGHIỆM RƠI RA
    // ==========================================
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
        
        // [FIX #5 & #6]: Nếu chết rồi thì không Knockback và hủy Invoke
        if (isDead || currentHealth <= 0) 
        {
            CancelInvoke(nameof(ResetColor));
            return;
        }

        if (sr != null)
        {
            sr.color = Color.red;
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
        if (sr != null) sr.color = Color.white; 
    }

    [Header("Combat References")]
    [SerializeField] protected GameObject attackHitbox;
    public void EnableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(true); }
    public void DisableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(false); }
}