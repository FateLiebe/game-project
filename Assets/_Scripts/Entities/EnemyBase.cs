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
    // HỆ THỐNG COOLDOWN KỸ NĂNG (MỚI THÊM)
    // ==========================================
    [Header("--- ATTACK COOLDOWNS ---")]
    [Tooltip("Khai báo CD cho từng chiêu. Normal 1 chiêu, Elite 2 chiêu...")]
    public float[] attackCooldowns = new float[] { 2f }; // Mặc định có 1 đòn, CD 2 giây
    
    // Mảng lưu trữ thời điểm (Time.time) mà đòn đánh thứ [i] được phép tung ra lần tiếp theo
    private float[] nextAttackTimes;

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

        // Khởi tạo độ dài mảng theo dõi CD bằng đúng với số lượng đòn đánh đã cài đặt trên Inspector
        nextAttackTimes = new float[attackCooldowns.Length];
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

        // Nếu thời gian hiện tại đã trôi qua mốc chờ -> Trả về true (Được phép đánh)
        return Time.time >= nextAttackTimes[attackIndex];
    }

    // Hàm này được gọi NGAY SAU KHI quái vật vung đòn thành công (để bắt đầu đếm ngược CD)
    public void RecordAttackUsage(int attackIndex)
    {
        if (attackIndex >= 0 && attackIndex < attackCooldowns.Length)
        {
            // Mốc đánh tiếp theo = Thời điểm hiện tại + Số giây cần chờ
            nextAttackTimes[attackIndex] = Time.time + attackCooldowns[attackIndex];
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
        float finalExp = Mathf.Round(baseExp * randomFactor);

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