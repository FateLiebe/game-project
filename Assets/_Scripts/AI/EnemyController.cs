using UnityEngine;
using System.Collections;

/// <summary>
/// Trí tuệ nhân tạo cơ bản cho Quái vật thường và Quái tinh anh (Elite). 
/// Sử dụng State Machine (Tuần tra, Rượt đuổi, Tấn công) thay vì Behavior Tree của Boss.
/// </summary>
public class EnemyController : EnemyBase
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }

    // ==========================================
    #region CONFIGURATION
    // ==========================================

    [Header("AI State")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Movement & Vision Settings")]
    [Tooltip("Sử dụng chỉ số từ EnemyDataSO ở EnemyBase")]
    private float patrolSpeed    => enemyData != null ? enemyData.patrolSpeed : 2f;
    private float chaseSpeed     => enemyData != null ? enemyData.chaseSpeed : 3.5f;
    private float lineOfSight    => enemyData != null ? enemyData.lineOfSight : 6f;
    private float attackRange    => enemyData != null ? enemyData.attackRange : 1.8f;
    private float idleDuration   => enemyData != null ? enemyData.idleDuration : 1.5f;

    [Tooltip("Tích vào nếu ảnh gốc của quái đang quay sang Trái")]
    [SerializeField] private bool spriteFacesLeft = false;

    [Header("--- CÀI ĐẶT LẬT HÌNH VFX ---")]
    [Tooltip("Tích vào nếu ảnh gốc của VFX đang quay sang Trái")]
    public bool isVfxFacingLeftDefault = true;

    [Header("--- ELITE EXTRA SETTINGS ---")]
    private float rangedAttackRange  => enemyData != null ? enemyData.rangedAttackRange : 6f;
    [SerializeField] private Material eliteOutlineMaterial;

    [Header("Đòn Gần — VFX Prefab (nếu có, ưu tiên hơn hitbox)")]
    [SerializeField] private GameObject meleeVFXPrefab;
    [SerializeField] private GameObject meleeSpawnPoint;

    [Header("Đòn Xa — Projectile Prefab (nếu có, ưu tiên hơn hitbox)")]
    [SerializeField] private GameObject rangedProjectilePrefab;
    [SerializeField] private GameObject rangedSpawnPoint;

    [Header("Detection")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private LayerMask playerLayer;

    #endregion

    // ==========================================
    #region INTERNAL STATE
    // ==========================================

    private Animator anim;
    private Transform playerTarget;
    private float stateTimer;
    private int   facingDir = 1;
    private bool  canAction = true;
    private int   currentAttackIndex = 0;
    private bool  isAttackVFXAllowed = false;
    private Vector2 _prevPosition;   // Dùng để tính tốc độ thực tế cho Animator

    /// <summary>Vị trí ghi nhớ của quái Tinh Anh (Elite) khi quyết định lùi ra xa để sử dụng kỹ năng tầm xa.</summary>
    private Vector2 rangedRepositionTargetPos;
    private bool    isRepositioning = false;

    #endregion

    // ==========================================
    #region UNITY LIFECYCLE
    // ==========================================

    protected override void Start()
    {
        base.Start();
        if (anim == null) anim = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (rank == EnemyRank.Elite && eliteOutlineMaterial != null && sr != null)
            sr.material = eliteOutlineMaterial;

        // Snap to ground on spawn (dùng groundLayerMask thừa kế từ EnemyBase)
        if (!isFlying)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 10f, groundLayerMask);
            if (hit.collider != null)
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y;
                transform.position = pos;
            }
        }

        UpdateFacingVisual();
        SwitchState(EnemyState.Patrol);
        _prevPosition = rb.position;
    }

    /// <summary>
    /// Ghi đè hàm OnEnable để thiết lập lại các giá trị mặc định khi Quái được lấy ra từ Object Pool.
    /// Giúp quái không bị lưu lại trạng thái đã chết từ lần spawn trước.
    /// </summary>
    protected override void OnEnable()
    {
        // Reset state for pooling
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }
        
        SwitchState(EnemyState.Idle);
        canAction = true;
        isAttackVFXAllowed = false;
        playerTarget = null;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Vòng lặp chính của AI. Đóng băng tư duy nếu đang dính hiệu ứng (Hurt/Dead) hoặc bị Time Stop.
    /// </summary>
    protected override void Update()
    {
        base.Update(); // CD countdown + ApplyGravity

        if (currentState == EnemyState.Dead || !canAction) return;

        switch (currentState)
        {
            case EnemyState.Idle:   UpdateIdle();   break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: break; // handled by coroutine
        }

        UpdateAnimations();
    }

    #endregion

    // ==========================================
    #region MOVEMENT (KINEMATIC)
    // ==========================================

    /// <summary>
    /// Di chuyển ngang an toàn bằng Kinematic. Tự động dừng lại nếu phía trước là vực thẳm hoặc tường, tránh lỗi lọt map.
    /// </summary>
    private void MoveHorizontal(float speed, int moveDir)
    {
        if (isKnockedBack) return;

        float step = speed * timeMultiplier * Time.deltaTime;

        // 1. Kiểm tra va chạm với Player: Tránh việc quái vật đẩy lùi Player (Overlap)
        if (IsPlayerBlockingPath(moveDir, step + 0.35f))
        {
            HandleBlockedByPlayer();
            return;
        }

        // 2. Kiểm tra vách tường phía trước bằng Raycast
        if (wallCheck != null &&
            Physics2D.Raycast(wallCheck.position, Vector2.right * moveDir, 0.2f, groundLayerMask))
            return;

        // 3. Kiểm tra mép vực: Đảm bảo quái (không bay) không tự rơi xuống vực
        if (!isFlying && ledgeCheck != null &&
            !Physics2D.Raycast(ledgeCheck.position, Vector2.down, 0.5f, groundLayerMask))
            return;

        transform.position += Vector3.right * (step * moveDir);
    }

    /// <summary>Raycast theo hướng moveDir để kiểm tra có Player không.</summary>
    private bool IsPlayerBlockingPath(int moveDir, float distance)
    {
        return Physics2D.Raycast(
            rb.position + Vector2.up * 0.5f,
            Vector2.right * moveDir,
            distance,
            playerLayer);
    }

    /// <summary>Hành vi khi bị Player chặn: tấn công ngay nếu sẵn sàng, không thì đứng yên.</summary>
    private void HandleBlockedByPlayer()
    {
        if (CanAttack(0))
        {
            currentAttackIndex = 0;
            SwitchState(EnemyState.Attack);
        }
        // Nếu CD chưa xong: đứng yên, chờ (không đẩy player)
    }

    #endregion

    // ==========================================
    #region AI STATES
    // ==========================================

    /// <summary>
    /// Trạng thái Đứng yên (Idle): Quái quan sát xung quanh trong một khoảng thời gian trước khi quay đầu tuần tra tiếp.
    /// Nếu phát hiện Player trong lúc này, lập tức chuyển sang Rượt đuổi hoặc Tấn công.
    /// </summary>
    private void UpdateIdle()
    {
        // Kinematic: không cần clear velocity, chỉ không gọi MoveHorizontal
        if (DetectPlayer())
        {
            float dist = Vector2.Distance(transform.position, playerTarget.position);
            if (CanUseAnyAttack(dist, out int attackToUse)) { currentAttackIndex = attackToUse; SwitchState(EnemyState.Attack); }
            else SwitchState(EnemyState.Chase);
            return;
        }
        stateTimer -= Time.deltaTime * timeMultiplier;
        if (stateTimer <= 0) { Flip(); SwitchState(EnemyState.Patrol); }
    }

    /// <summary>
    /// Trạng thái Tuần tra (Patrol): Quái di chuyển đều đặn về phía trước.
    /// Nếu đụng tường hoặc sắp rơi xuống vực, nó sẽ dừng lại (Idle) rồi quay đầu.
    /// </summary>
    private void UpdatePatrol()
    {
        if (CheckWall() || !CheckLedge()) { SwitchState(EnemyState.Idle); return; }
        MoveHorizontal(patrolSpeed, facingDir);
        if (DetectPlayer()) SwitchState(EnemyState.Chase);
    }

    /// <summary>
    /// Đuổi theo người chơi. Đặc biệt quái Elite biết lùi lại để giữ khoảng cách ném chưởng (Ranged Attack).
    /// </summary>
    private void UpdateChase()
    {
        if (playerTarget == null) { SwitchState(EnemyState.Idle); return; }

        BaseEntity targetEntity = playerTarget.GetComponentInParent<BaseEntity>();
        if (targetEntity == null || targetEntity.currentHealth <= 0)
        {
            playerTarget = null;
            SwitchState(EnemyState.Idle);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distToPlayer > lineOfSight * 1.5f) { playerTarget = null; isRepositioning = false; SwitchState(EnemyState.Idle); return; }

        // Hành vi ưu tiên của quái Tinh Anh (Elite): Tấn công cận chiến nếu mục tiêu nằm trong tầm gần
        if (rank == EnemyRank.Elite && distToPlayer <= attackRange && CanAttack(0))
        {
            isRepositioning = false;
            currentAttackIndex = 0;
            SwitchState(EnemyState.Attack);
            return;
        }

        // Hành vi Rút lui chiến thuật của quái Tinh Anh (Elite): Lùi ra để sử dụng kỹ năng đánh xa (Ranged Attack)
        if (rank == EnemyRank.Elite && !isRepositioning
            && distToPlayer > attackRange && !CanAttack(0) && CanAttack(1))
        {
            rangedRepositionTargetPos = playerTarget.position;
            isRepositioning = true;
        }

        // Thực thi quá trình lùi ra xa (Tactical Retreat) của quái Tinh Anh
        if (rank == EnemyRank.Elite && isRepositioning)
        {
            float distToSnapshot = Vector2.Distance(transform.position, rangedRepositionTargetPos);
            if (distToSnapshot >= rangedAttackRange * 0.9f)
            {
                isRepositioning = false;
                if (CanUseAnyAttack(distToPlayer, out int rangedAttack))
                {
                    currentAttackIndex = rangedAttack;
                    SwitchState(EnemyState.Attack);
                    return;
                }
            }
            else
            {
                FacePlayer();
                int retreatDir = (rangedRepositionTargetPos.x > transform.position.x) ? -1 : 1;
                // Lùi ra an toàn: Cố gắng duy trì khoảng cách nhưng sẽ dừng lại nếu đụng tường hoặc mép vực
                if (!CheckLedge() || CheckWall())
                    isRepositioning = false;
                else
                    MoveHorizontal(chaseSpeed, retreatDir);
                return;
            }
        }

        // Chế độ Rượt đuổi (Chase) thông thường dành cho mọi cấp bậc quái
        if (CanUseAnyAttack(distToPlayer, out int attackToUse))
        {
            currentAttackIndex = attackToUse;
            SwitchState(EnemyState.Attack);
            return;
        }

        FacePlayer();
        if (distToPlayer > attackRange && CheckLedge() && !CheckWall())
            MoveHorizontal(chaseSpeed, facingDir);
        // Nếu trong tầm hoặc bị chặn địa hình: đứng yên (không gọi MoveHorizontal)
    }

    private bool CanUseAnyAttack(float dist, out int attackToUse)
    {
        attackToUse = -1;
        if (dist <= attackRange && CanAttack(0)) { attackToUse = 0; return true; }
        if (rank == EnemyRank.Elite && !isRepositioning
            && dist > attackRange && dist <= rangedAttackRange && CanAttack(1))
        { attackToUse = 1; return true; }
        return false;
    }

    #endregion

    // ==========================================
    #region ATTACK
    // ==========================================

    /// <summary>
    /// Tiến hành thực thi đòn tấn công. Khóa di chuyển và chuyển sang trạng thái chờ Animation.
    /// </summary>
    private IEnumerator PerformAttack()
    {
        canAction = false;
        isAttackVFXAllowed = true;
        // Kinematic: không cần clear velocity, chỉ không move

        if (anim != null) { anim.SetInteger("attackType", currentAttackIndex); anim.SetTrigger("Attack"); }
        RecordAttackUsage(currentAttackIndex);

        float timer = 0f;
        while (timer < 1f) { timer += Time.deltaTime * timeMultiplier; yield return null; }

        isAttackVFXAllowed = false;
        CancelCurrentAttackHitbox();
        canAction = true;
        SwitchState(EnemyState.Chase);
    }

    #endregion

    // ==========================================
    #region ANIMATION EVENTS
    // ==========================================

    public new void EnableHitbox()        { if (attackHitbox       != null) attackHitbox.SetActive(true); }
    public new void DisableHitbox()       { if (attackHitbox       != null) attackHitbox.SetActive(false); }
    public void EnableRangedHitbox()  { if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(true); }
    public void DisableRangedHitbox() { if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(false); }

    public void TriggerMeleeAttackVFX()
    {
        if (!isAttackVFXAllowed || meleeVFXPrefab == null) return;
        Vector3 spawnPos = meleeSpawnPoint != null ? meleeSpawnPoint.transform.position : transform.position;
        GameObject vfx;
        if (ObjectPoolManager.Instance != null) vfx = ObjectPoolManager.Instance.Get(meleeVFXPrefab, spawnPos, Quaternion.identity);
        else vfx = Instantiate(meleeVFXPrefab, spawnPos, Quaternion.identity);

        float finalFacing = isVfxFacingLeftDefault ? -facingDir : facingDir;
        Vector3 vfxScale  = vfx.transform.localScale;
        vfxScale.x        = Mathf.Abs(vfxScale.x) * finalFacing;
        vfx.transform.localScale = vfxScale;

        UniversalHitbox hb = vfx.GetComponent<UniversalHitbox>();
        if (hb != null) { hb.owner = this.gameObject; hb.damageOverride = Attack; }
    }

    public void TriggerRangedAttackVFX()
    {
        if (!isAttackVFXAllowed || rangedProjectilePrefab == null || playerTarget == null) return;
        Vector3 spawnPos = rangedSpawnPoint != null ? rangedSpawnPoint.transform.position : transform.position;
        GameObject vfx;
        if (ObjectPoolManager.Instance != null) vfx = ObjectPoolManager.Instance.Get(rangedProjectilePrefab, spawnPos, Quaternion.identity);
        else vfx = Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity);

        Projectile proj  = vfx.GetComponent<Projectile>();
        if (proj != null) proj.SetTarget(playerTarget);

        UniversalHitbox hb = vfx.GetComponent<UniversalHitbox>();
        if (hb != null) { hb.owner = this.gameObject; hb.damageOverride = Attack; }
    }

    public void CancelCurrentAttackHitbox()
    {
        if (attackHitbox       != null) attackHitbox.SetActive(false);
        if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(false);
    }

    #endregion

    // ==========================================
    #region DAMAGE & DEATH
    // ==========================================

    /// <summary>
    /// Kích hoạt khi quái nhận sát thương.
    /// Cập nhật mục tiêu là kẻ vừa đánh mình (Player) và có tỷ lệ bị khựng lại (Hurt Stagger) nếu bị đánh trúng điểm yếu.
    /// </summary>
    public override void ApplyDamage(DamageInfo info)
    {
        // Lưu knockback trước khi base có thể start coroutine
        Vector2 knockbackForce = info.knockbackForce;

        base.ApplyDamage(info); // → gọi EnemyBase.ApplyDamage → StartKnockback

        if (currentHealth > 0)
        {
            isRepositioning = false;
            CancelCurrentAttackHitbox();
            StopAllCoroutines(); // Dừng tất cả kể cả KnockbackRoutine vừa start

            // Restart knockback sau StopAll (không bị kill lần này)
            StartKnockback(knockbackForce);

            SwitchState(EnemyState.Hurt);
            if (anim != null) { anim.ResetTrigger("Attack"); anim.SetTrigger("Hurt"); }
            if (info.attacker != null) { playerTarget = info.attacker.transform; FacePlayer(); }
            StartCoroutine(RecoverFromHurt());
        }
    }

    /// <summary>
    /// Thời gian bị choáng/khựng lại khi nhận sát thương chí mạng hoặc bị dồn sát thương lớn.
    /// </summary>
    private IEnumerator RecoverFromHurt()
    {
        canAction = false;
        float timer = 0f;
        while (timer < 0.4f) { timer += Time.deltaTime * timeMultiplier; yield return null; }
        canAction = true;
        SwitchState(EnemyState.Chase);
    }

    /// <summary>
    /// Quái chết. Ngắt toàn bộ Coroutine, vô hiệu hóa Hitbox vật lý để Player không kẹt, và tiến hành dọn dẹp.
    /// </summary>
    protected override void Die()
    {
        CancelCurrentAttackHitbox();
        StopAllCoroutines();
        SwitchState(EnemyState.Dead);
        if (anim != null) anim.SetBool("isDead", true);

        GetComponent<Collider2D>().enabled = false;

        // Quái bay: chuyển về Dynamic để ngã xuống sau khi chết
        if (isFlying && rb != null)
        {
            rb.bodyType    = RigidbodyType2D.Dynamic;
            rb.gravityScale = 2f;
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.833f);
        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }

    #endregion

    // ==========================================
    #region HELPERS & DETECTION
    // ==========================================

    /// <summary>
    /// Quản lý việc chuyển đổi giữa các State AI (Idle -> Patrol -> Chase -> Attack).
    /// Hỗ trợ reset các biến liên quan đến hoạt ảnh và di chuyển để tránh kẹt trạng thái.
    /// </summary>
    private void SwitchState(EnemyState newState)
    {
        currentState = newState;
        if (newState == EnemyState.Idle)   stateTimer = idleDuration;
        else if (newState == EnemyState.Attack) StartCoroutine(PerformAttack());
    }

    private void UpdateFacingVisual()
    {
        Vector3 scale = transform.localScale;
        scale.x       = spriteFacesLeft ? -facingDir * Mathf.Abs(scale.x) : facingDir * Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void Flip()       { facingDir *= -1; UpdateFacingVisual(); }

    private void FacePlayer()
    {
        if (playerTarget == null) return;
        if (playerTarget.position.x > transform.position.x && facingDir == -1) Flip();
        else if (playerTarget.position.x < transform.position.x && facingDir == 1) Flip();
    }

    private bool CheckWall()
    {
        if (wallCheck == null) return false;
        return Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, 0.2f, groundLayerMask);
    }

    private bool CheckLedge()
    {
        if (isFlying)        return true;
        if (ledgeCheck == null) return true;
        return Physics2D.Raycast(ledgeCheck.position, Vector2.down, 0.5f, groundLayerMask);
    }

    private bool DetectPlayer()
    {
        if (wallCheck != null)
        {
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, lineOfSight, playerLayer);
            if (hit.collider != null)
            {
                BaseEntity target = hit.collider.GetComponentInParent<BaseEntity>();
                if (target != null && target.currentHealth > 0) { playerTarget = target.transform; return true; }
            }
        }
        Collider2D closeHit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (closeHit != null)
        {
            BaseEntity target = closeHit.GetComponentInParent<BaseEntity>();
            if (target != null && target.currentHealth > 0) { playerTarget = target.transform; return true; }
        }
        return false;
    }

    private void UpdateAnimations()
    {
        if (anim == null) return;
        // Kinematic: tính tốc độ từ delta position thay vì linearVelocity
        float speed = Mathf.Abs(rb.position.x - _prevPosition.x) / Time.deltaTime;
        _prevPosition = rb.position;
        if (!isFlying) anim.SetFloat("speed", speed);
    }

    private void OnDrawGizmos()
    {
        if (wallCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * facingDir * lineOfSight);
        }
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
    }

    #endregion
}