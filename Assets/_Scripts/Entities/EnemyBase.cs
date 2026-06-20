using UnityEngine;
using System.Collections;

/// <summary>
/// Lớp cha của mọi Enemy. Quản lý: Chỉ số cơ bản, Thời gian hồi chiêu (CD), Di chuyển Kinematic.
/// Cung cấp hàm Đẩy Lùi (Knockback) và Trọng lực (Gravity) giả lập để xử lý vật lý an toàn hơn Rigidbody2D.
/// </summary>
public class EnemyBase : BaseEntity 
{
    public enum EnemyRank { Normal, Elite, Boss }

    // ==========================================
    #region CONFIGURATION
    // ==========================================

    [Header("--- ENEMY CONFIG ---")]
    public EnemyRank rank = EnemyRank.Normal;

    [Header("EXP Reward")]
    [SerializeField] protected float expMultiplier = 10f;

    [Header("--- ATTACK COOLDOWNS ---")]
    [Tooltip("Khai báo CD cho từng chiêu. Normal 1 chiêu, Elite 2 chiêu...")]
    public float[] attackCooldowns = new float[] { 3f };
    protected float[] currentAttackCooldowns;

    // Chuyển từ EnemyController lên đây để KnockbackRoutine truy cập được
    [Header("--- MOVEMENT BASE ---")]
    [Tooltip("Tích vào nếu là quái bay")]
    public bool isFlying = false;
    [SerializeField] protected LayerMask groundLayerMask;

    #endregion

    // ==========================================
    #region INTERNAL REFS & STATE
    // ==========================================

    protected SpriteRenderer sr;
    protected Rigidbody2D rb;
    protected Color defaultColor = Color.white;

    // Kinematic manual gravity
    private float _verticalVel = 0f;
    private const float GRAVITY_ACCEL   = -22f;
    private const float MAX_FALL_SPEED  = -22f;
    private const float GROUND_RAY_DIST =  0.2f;

    // Knockback state — protected agar EnemyController có thể đọc
    protected bool isKnockedBack = false;

    // Base stats (scale theo level)
    private const float BASE_HP  = 150f;
    private const float BASE_ATK =  12f;
    private const float BASE_DEF =   3f;

    public override float MaxHealth => BASE_HP  + ((currentLevel - 1) * 30f);
    public override float Attack    => BASE_ATK + ((currentLevel - 1) *  4f);
    public override float Defense   => BASE_DEF + ((currentLevel - 1) *  1f);

    #endregion

    // ==========================================
    #region UNITY LIFECYCLE
    // ==========================================

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // Chuyển sang Kinematic — mọi di chuyển do script kiểm soát
        if (rb != null)
        {
            rb.bodyType    = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        if (rank == EnemyRank.Elite)
        {
            defaultColor = new Color(1f, 0.5f, 0.5f);
            if (sr != null) sr.color = defaultColor;
        }

        currentAttackCooldowns = new float[attackCooldowns.Length];
    }

    protected virtual void Update()
    {
        // Đếm ngược CD skill — nhân timeMultiplier để đóng băng khi Time Stop
        if (currentAttackCooldowns != null)
        {
            for (int i = 0; i < currentAttackCooldowns.Length; i++)
                if (currentAttackCooldowns[i] > 0)
                    currentAttackCooldowns[i] -= Time.deltaTime * timeMultiplier;
        }

        ApplyGravity();
    }

    #endregion

    // ==========================================
    #region KINEMATIC GRAVITY
    // ==========================================

    /// <summary>
    /// Giả lập trọng lực thủ công cho Kinematic Body bằng Raycast.
    /// Giúp quái rơi từ từ xuống đất mượt mà thay vì dùng Rigidbody2D.Dynamic gây giật lag.
    /// </summary>
    protected void ApplyGravity()
    {
        if (isFlying || isKnockedBack || rb == null) return;

        // Raycast xuống dưới từ chân để kiểm tra mặt đất
        bool grounded = Physics2D.Raycast(
            rb.position + Vector2.up * 0.05f,
            Vector2.down,
            GROUND_RAY_DIST,
            groundLayerMask);

        if (grounded)
        {
            _verticalVel = 0f;
        }
        else
        {
            _verticalVel += GRAVITY_ACCEL * Time.deltaTime;
            _verticalVel  = Mathf.Max(_verticalVel, MAX_FALL_SPEED);
            transform.position += Vector3.up * (_verticalVel * Time.deltaTime);
        }
    }

    #endregion

    // ==========================================
    #region KNOCKBACK (SAFE — KINEMATIC)
    // ==========================================

    /// <summary>
    /// Xử lý lực đẩy lùi (Knockback) an toàn bằng Coroutine.
    /// Tự động kiểm tra hẻm vực/tường phía sau để chặn lại, tránh tình trạng quái bị văng ra khỏi bản đồ.
    /// </summary>
    private Coroutine knockbackCoroutine;

    public void StartKnockback(Vector2 force)
    {
        if (rb == null) return;
        
        // Vô hiệu hóa knockback nếu đang trong Time Stop
        if (timeMultiplier < 1f) return;

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        isKnockedBack = false;
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(force));
    }

    private IEnumerator KnockbackRoutine(Vector2 force)
    {
        isKnockedBack = true;

        float xForce = force.x;
        if (Mathf.Abs(xForce) < 0.1f) { isKnockedBack = false; yield break; }

        Vector2 dir  = new Vector2(Mathf.Sign(xForce), 0f);
        float speed  = Mathf.Abs(xForce);
        float duration = 0.3f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            if (rb == null) break;

            float t    = 1f - (elapsed / duration); // giảm tốc tuyến tính
            float step = speed * t * Time.deltaTime;

            // [A] Kiểm tra tường phía knockback (ngang, cao ngang ngực)
            if (Physics2D.Raycast(rb.position + Vector2.up * 0.4f, dir, step + 0.12f, groundLayerMask))
                break;

            // [B] Kiểm tra mép vực phía knockback (chỉ cho quái đi bộ)
            if (!isFlying)
            {
                Vector2 futurePos = (Vector2)transform.position + dir * step;
                bool hasGround    = Physics2D.Raycast(futurePos + Vector2.up * 0.05f, Vector2.down, 0.5f, groundLayerMask);
                if (!hasGround) break;
            }

            transform.position += (Vector3)(dir * step);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isKnockedBack = false;
    }

    #endregion

    // ==========================================
    #region COMBAT
    // ==========================================

    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info);

        if (isDead || currentHealth <= 0)
        {
            CancelInvoke(nameof(ResetColor));
            return;
        }

        // Nhấp nháy đỏ khi bị chém
        if (sr != null)
        {
            sr.color = Color.red;
            Invoke(nameof(ResetColor), 0.1f);
        }

        // Knockback an toàn qua coroutine
        StartKnockback(info.knockbackForce);
    }

    private void ResetColor()
    {
        if (sr != null) sr.color = defaultColor;
    }

    public bool CanAttack(int attackIndex)
    {
        if (attackIndex < 0 || attackIndex >= attackCooldowns.Length) return false;
        return currentAttackCooldowns[attackIndex] <= 0f;
    }

    public void RecordAttackUsage(int attackIndex)
    {
        if (attackIndex >= 0 && attackIndex < attackCooldowns.Length)
            currentAttackCooldowns[attackIndex] = attackCooldowns[attackIndex];
    }

    #endregion

    // ==========================================
    #region STATS & EXP
    // ==========================================

    protected override void InitializeStats()
    {
        currentHealth = MaxHealth;
        base.InitializeStats();
    }

    public float GetExpReward()
    {
        float baseExp = currentLevel * expMultiplier;
        switch (rank)
        {
            case EnemyRank.Elite: baseExp *= 2f; break;
            case EnemyRank.Boss:  baseExp *= 5f; break;
        }
        float finalExp = Mathf.Round(baseExp * Random.Range(0.85f, 1.15f) * 1000f) / 1000f;
        if (Random.value < 0.05f)
        {
            finalExp *= 2f;
            Debug.Log("<color=yellow>JACKPOT! X2 EXP TỪ QUÁI VẬT!</color>");
        }
        return finalExp;
    }

    #endregion

    // ==========================================
    #region HITBOX HELPERS
    // ==========================================

    [Header("Combat References")]
    [SerializeField] protected GameObject attackHitbox;
    [SerializeField] protected GameObject rangedAttackHitbox;

    public void EnableHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.SetActive(true);
            CancelInvoke(nameof(DisableHitbox));
            Invoke(nameof(DisableHitbox), 0.5f);
        }
    }

    public void DisableHitbox()
    {
        if (attackHitbox != null) attackHitbox.SetActive(false);
    }

    #endregion
}