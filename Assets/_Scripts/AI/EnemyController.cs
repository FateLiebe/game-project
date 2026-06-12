using UnityEngine;
using System.Collections;

public class EnemyController : EnemyBase
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }

    [Header("AI State")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Movement & Vision Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float lineOfSight = 6f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float idleDuration = 1.5f;

    [Tooltip("Tích vào nếu ảnh gốc của quái đang quay sang Trái")]
    [SerializeField] private bool spriteFacesLeft = false;

    [Tooltip("Tích vào nếu là quái bay")]
    [SerializeField] public bool isFlying = false;

    [Header("--- CÀI ĐẶT LẬT HÌNH VFX ---")]
    [Tooltip("Tích vào nếu ảnh gốc của VFX đang quay sang Trái")]
    public bool isVfxFacingLeftDefault = true;

    [Header("--- ELITE EXTRA SETTINGS ---")]
    [SerializeField] private float rangedAttackRange = 6f;
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
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    private Animator anim;
    private Transform playerTarget;
    private float stateTimer;
    private int facingDir = 1;
    private bool canAction = true;
    private int currentAttackIndex = 0;

    protected override void Start()
    {
        base.Start();
        anim = GetComponent<Animator>();

        if (rank == EnemyRank.Elite && eliteOutlineMaterial != null && sr != null)
            sr.material = eliteOutlineMaterial;

        if (isFlying)
        {
            if (rb != null) rb.gravityScale = 0f;
        }
        else
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 10f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y;
                transform.position = pos;
            }
        }

        UpdateFacingVisual();
        SwitchState(EnemyState.Patrol);
    }

    protected override void Update()
    {
        base.Update();
        if (currentState == EnemyState.Dead || !canAction) return;

        switch (currentState)
        {
            case EnemyState.Idle:   UpdateIdle();   break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: break;
        }
        UpdateAnimations();
    }

    private bool CanUseAnyAttack(float dist, out int attackToUse)
    {
        attackToUse = -1;

        // Đòn gần luôn ưu tiên tuyệt đối khi trong tầm
        if (dist <= attackRange && CanAttack(0))
        {
            attackToUse = 0;
            return true;
        }

        // Đòn xa chỉ dùng khi NGOÀI tầm gần và là Elite
        if (rank == EnemyRank.Elite && dist > attackRange && dist <= rangedAttackRange && CanAttack(1))
        {
            attackToUse = 1;
            return true;
        }

        return false;
    }

    private void UpdateIdle()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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

    private void UpdatePatrol()
    {
        if (CheckWall() || !CheckLedge()) { SwitchState(EnemyState.Idle); return; }
        rb.linearVelocity = new Vector2(patrolSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);
        if (DetectPlayer()) SwitchState(EnemyState.Chase);
    }

    private void UpdateChase()
    {
        if (playerTarget == null) { SwitchState(EnemyState.Idle); return; }

        BaseEntity targetEntity = playerTarget.GetComponentInParent<BaseEntity>();
        if (targetEntity == null || targetEntity.currentHealth <= 0) { playerTarget = null; SwitchState(EnemyState.Idle); return; }

        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distToPlayer > lineOfSight * 1.5f) { playerTarget = null; SwitchState(EnemyState.Idle); return; }

        if (CanUseAnyAttack(distToPlayer, out int attackToUse)) { currentAttackIndex = attackToUse; SwitchState(EnemyState.Attack); return; }

        FacePlayer();
        float stopDistance = (rank == EnemyRank.Elite && CanAttack(1)) ? rangedAttackRange : attackRange;
        if (distToPlayer <= stopDistance || !CheckLedge() || CheckWall())
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(chaseSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);
    }

    private IEnumerator PerformAttack()
    {
        canAction = false;
        rb.linearVelocity = Vector2.zero;

        if (anim != null) { anim.SetInteger("attackType", currentAttackIndex); anim.SetTrigger("Attack"); }

        RecordAttackUsage(currentAttackIndex);

        float timer = 0f;
        while (timer < 1f) { timer += Time.deltaTime * timeMultiplier; yield return null; }

        CancelCurrentAttackHitbox();
        canAction = true;
        SwitchState(EnemyState.Chase);
    }

    // ==========================================
    // ANIMATION EVENTS — Gắn vào keyframe trong Animator
    // ==========================================

    // Loại 1 & 3: Hitbox thuần từ animation (gần)
    public void EnableHitbox()  { if (attackHitbox != null) attackHitbox.SetActive(true); }
    public void DisableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(false); }

    // Loại 1: Hitbox thuần từ animation (xa)
    public void EnableRangedHitbox()  { if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(true); }
    public void DisableRangedHitbox() { if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(false); }

    // Loại 2 & 3: VFX đòn gần
    public void TriggerMeleeAttackVFX()
    {
        if (meleeVFXPrefab == null) return;
        Vector3 spawnPos = meleeSpawnPoint != null ? meleeSpawnPoint.transform.position : transform.position;
        GameObject vfx = Instantiate(meleeVFXPrefab, spawnPos, Quaternion.identity);

        float finalFacing = isVfxFacingLeftDefault ? -facingDir : facingDir;
        Vector3 vfxScale = vfx.transform.localScale;
        vfxScale.x = Mathf.Abs(vfxScale.x) * finalFacing;
        vfx.transform.localScale = vfxScale;

        UniversalHitbox hb = vfx.GetComponent<UniversalHitbox>();
        if (hb != null) { hb.owner = this.gameObject; hb.damageOverride = Attack; }
    }

    // Loại 2 & 3: VFX đòn xa (Projectile)
    public void TriggerRangedAttackVFX()
    {
        if (rangedProjectilePrefab == null || playerTarget == null) return;
        Vector3 spawnPos = rangedSpawnPoint != null ? rangedSpawnPoint.transform.position : transform.position;
        GameObject vfx = Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity);

        // Projectile tự xoay theo rotation — không set scale ở đây

        Projectile proj = vfx.GetComponent<Projectile>();
        if (proj != null) proj.SetTarget(playerTarget);

        UniversalHitbox hb = vfx.GetComponent<UniversalHitbox>();
        if (hb != null) { hb.owner = this.gameObject; hb.damageOverride = Attack; }
    }

    public void CancelCurrentAttackHitbox()
    {
        if (attackHitbox != null) attackHitbox.SetActive(false);
        if (rangedAttackHitbox != null) rangedAttackHitbox.SetActive(false);
    }

    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info);
        if (currentHealth > 0)
        {
            CancelCurrentAttackHitbox();
            StopAllCoroutines();
            SwitchState(EnemyState.Hurt);
            if (anim != null) { anim.ResetTrigger("Attack"); anim.SetTrigger("Hurt"); }
            if (info.attacker != null) { playerTarget = info.attacker.transform; FacePlayer(); }
            StartCoroutine(RecoverFromHurt());
        }
    }

    private IEnumerator RecoverFromHurt()
    {
        canAction = false;
        float timer = 0f;
        while (timer < 0.4f) { timer += Time.deltaTime * timeMultiplier; yield return null; }
        canAction = true;
        SwitchState(EnemyState.Chase);
    }

    protected override void Die()
    {
        CancelCurrentAttackHitbox();
        StopAllCoroutines();
        SwitchState(EnemyState.Dead);
        anim.SetBool("isDead", true);
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;
        if (isFlying && rb != null) rb.gravityScale = 2f;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.833f);
        Destroy(gameObject);
    }

    private void SwitchState(EnemyState newState)
    {
        currentState = newState;
        if (newState == EnemyState.Idle) stateTimer = idleDuration;
        else if (newState == EnemyState.Attack) StartCoroutine(PerformAttack());
    }

    private void UpdateFacingVisual()
    {
        Vector3 scale = transform.localScale;
        scale.x = spriteFacesLeft ? -facingDir * Mathf.Abs(scale.x) : facingDir * Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void Flip() { facingDir *= -1; UpdateFacingVisual(); }

    private void FacePlayer()
    {
        if (playerTarget == null) return;
        if (playerTarget.position.x > transform.position.x && facingDir == -1) Flip();
        else if (playerTarget.position.x < transform.position.x && facingDir == 1) Flip();
    }

    private bool CheckWall() { if (wallCheck == null) return false; return Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, 0.2f, groundLayer); }

    private bool CheckLedge()
    {
        if (isFlying) return true;
        if (ledgeCheck == null) return true;
        return Physics2D.Raycast(ledgeCheck.position, Vector2.down, 0.5f, groundLayer);
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
        if (!isFlying) anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
    }

    private void OnDrawGizmos()
    {
        if (wallCheck != null) { Gizmos.color = Color.red; Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * facingDir * lineOfSight); }
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f); Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
    }
}