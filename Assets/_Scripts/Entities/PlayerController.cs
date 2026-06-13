using UnityEngine;
using System.Collections;
using System.Collections.Generic; // [MỚI]: Dùng để chứa danh sách kẻ địch bị đưa vào "Sổ đen"

public class PlayerController : BaseEntity
{
    public enum PlayerState { Grounded, Airborne, Dashing, DashStalling, Attacking }

    [Header("State Machine")]
    [SerializeField] private PlayerState currentState = PlayerState.Airborne;

    [Header("Runtime Resources")]
    private int jumpsLeft;
    private int currentDashCharges;
    private float dashRechargeTimer;
    private int dashesUsedInAir = 0;
    private int comboStep = 0;

    [Header("State Flags")]
    private bool dashResetByJump = false; 
    private bool canAirAttack = true;
    public bool isPerfectDodge; 
    private bool isPhasingThrough = false; 

    [Header("Combat System")]
    [SerializeField] private GameObject attackHitbox;
    [SerializeField] private float[] attackDurations = new float[] { 0.3f, 0.4f, 0.6f }; 
    [SerializeField] private float[] comboDamageMultipliers = new float[] { 0.8f, 1.1f, 1.3f };
    [SerializeField] private float attackInputBufferTime = 0.2f; 
    [SerializeField] [Range(0f, 1f)] private float attackMovementMultiplier = 0.3f;
    
    [SerializeField] private float perfectDodgeWindow = 0.4f; 
    [SerializeField] private float perfectDodgeCooldown = 15f;
    
    private bool perfectDodgeTriggered;

    private float perfectDodgeTimer = 0f;
    private float currentAttackDuration = 0f;
    private bool isAttacking = false;
    private Hurtbox hurtbox;

    [Header("Physics & Detection")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Boss Detection")]
    public float bossDetectionRadius = 15f; 
    public LayerMask bossLayer; 
    private BaseEntity activeBoss;

    [Header("--- SUPPORT SKILL ---")]
    public ItemSO equippedSupportSkill; 
    private float supportSkillCDTimer = 0f;
    public int currentSupportSkillUses = 0; // Số lần dùng còn lại
    private bool isSupportSkillInitialized = false;
    
    private Animator anim;
    private Rigidbody2D rb;
    private Coroutine dashCoroutine;
    private Coroutine attackCoroutine;
    private Vector2 currentDashDirection; 
    private float lastAttackTime;
    private float lastAttackInputTime = -10f;
    private float lastSKeyPressTime;
    private float lastJumpTime = -10f;
    private float horizontalInput;
    private float verticalInput;
    private float originalGravity;
    
    private Collider2D[] threatColliders = new Collider2D[20];
    
    // Lưu BaseEntity để đồng nhất danh tính của kẻ địch
    private List<BaseEntity> ignoredAttackers = new List<BaseEntity>();

    protected override void Start()
    {
        base.Start(); 
        rb = GetComponent<Rigidbody2D>();
        hurtbox = GetComponentInChildren<Hurtbox>(); 
        anim = GetComponent<Animator>();
        originalGravity = rb.gravityScale;
        
        if (baseData != null) currentDashCharges = baseData.maxDashes;
    }

    private void Update()
    {
        HandleDashRecharge();
        if (perfectDodgeTimer > 0)
        {
            perfectDodgeTimer -= Time.deltaTime;
        }
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateDodgeCD(perfectDodgeTimer, perfectDodgeCooldown);
        }

        if (StatsUIManager.Instance != null && StatsUIManager.Instance.IsOpen)
        {
            horizontalInput = 0; 
            verticalInput = 0;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); 
            UpdateAnimations(); 
            return; 
        }

        HandleBossDetection();

        HandleSupportSkill();

        HandleInput();
        HandleComboReset();
        CheckGrounded();
        Flip();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;

        float targetSpeed = horizontalInput * currentMoveSpeed;
        if (isAttacking) targetSpeed *= attackMovementMultiplier;

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }

    private void HandleInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        bool isJumpingUp = currentState == PlayerState.Airborne && rb.linearVelocity.y > 0.1f;
        
        if (currentState != PlayerState.Dashing && !isJumpingUp)
        {
            if (Time.time - lastAttackInputTime <= attackInputBufferTime 
                && Time.time >= lastAttackTime + (currentAttackDuration * 0.8f)) 
            {
                if (currentState == PlayerState.Grounded || canAirAttack)
                {
                    if (currentState == PlayerState.DashStalling)
                    {
                        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
                        rb.gravityScale = originalGravity;
                        currentState = PlayerState.Airborne;
                    }
                    ExecuteAttack();
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            lastAttackInputTime = Time.time;
        }

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1)))
        {
            if (CanDash())
            {
                CancelAttack();
                if (dashCoroutine != null) StopCoroutine(dashCoroutine);
                rb.gravityScale = originalGravity; 
                
                Vector2 dashDir = Vector2.zero;
                bool isBackdash = false;

                if (horizontalInput == 0 && verticalInput == 0 && currentState == PlayerState.Grounded)
                {
                    float currentFacing = transform.localScale.x >= 0 ? 1f : -1f;
                    float dirToThreat = 0f;

                    if (CheckIncomingAttackFromBehind(out dirToThreat))
                    {
                        if (perfectDodgeTimer <= 0f)
                        {
                            dashDir = new Vector2(dirToThreat, 0);
                            isBackdash = true; 
                        }
                        else
                        {
                            Vector3 newScale = transform.localScale;
                            newScale.x = Mathf.Abs(newScale.x) * dirToThreat; 
                            transform.localScale = newScale;

                            dashDir = new Vector2(-dirToThreat, 0); 
                            isBackdash = true;
                        }
                    }
                    else
                    {
                        dashDir = new Vector2(-currentFacing, 0);
                        isBackdash = true;
                    }
                }
                else
                {
                    dashDir = CalculateDashDirection();
                    if (dashDir == Vector2.zero) dashDir = new Vector2(transform.localScale.x >= 0 ? 1f : -1f, 0);
                    isBackdash = (dashDir.x * transform.localScale.x) < 0;
                }

                currentDashDirection = dashDir; 
                dashCoroutine = StartCoroutine(PerformDash(dashDir, isBackdash));
                return; 
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && jumpsLeft > 0)
        {
            if (currentState == PlayerState.Grounded)
            {
                Jump();
            }
            else if (currentState == PlayerState.Airborne || currentState == PlayerState.DashStalling)
            {
                if (dashCoroutine != null) StopCoroutine(dashCoroutine); 
                dashResetByJump = true; 
                Jump();
            }
        }

        HandleFastFall();
    }

    private bool CheckIncomingAttackFromBehind(out float dirToThreat)
    {
        dirToThreat = 0f;
        float facingDir = transform.localScale.x >= 0 ? 1f : -1f;
        
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, 5f, threatColliders);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = threatColliders[i];
            if (col.transform.root == this.transform || col.CompareTag("Player")) continue;

            BaseEntity enemyEntity = col.GetComponentInParent<BaseEntity>();
            if (enemyEntity != null)
            {
                float direction = Mathf.Sign(enemyEntity.transform.position.x - transform.position.x);
                if (direction != facingDir && direction != 0)
                {
                    // [FIX #8]: So sánh Component thay vì String
                    bool isHitbox = col.GetComponent<UniversalHitbox>() != null && col.enabled;
                    
                    bool isPlayingAttackAnim = false;
                    Animator enemyAnim = enemyEntity.GetComponentInChildren<Animator>();
                    if (enemyAnim != null)
                    {
                        AnimatorStateInfo stateInfo = enemyAnim.GetCurrentAnimatorStateInfo(0);
                        isPlayingAttackAnim = stateInfo.IsName("Attack") || stateInfo.IsTag("Attack");
                    }

                    if (isHitbox || isPlayingAttackAnim)
                    {
                        dirToThreat = direction; 
                        return true; 
                    }
                }
            }
        }
        return false;
    }

    private Vector2 CalculateDashDirection()
    {
        float h = horizontalInput;
        float v = verticalInput;

        if (currentState == PlayerState.Grounded && v < 0) v = 0;
        if (h == 0 && v > 0) v = 0;

        return new Vector2(h, v).normalized; 
    }

    private void TryProactivePerfectDodge()
    {
        if (perfectDodgeTimer > 0f) return; 

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, 3.5f, threatColliders);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = threatColliders[i];
            
            if (col.transform.root == this.transform || col.CompareTag("Player")) continue;

            BaseEntity enemyEntity = col.GetComponentInParent<BaseEntity>();

            if (enemyEntity != null && enemyEntity.currentHealth > 0)
            {
                Animator enemyAnim = enemyEntity.GetComponentInChildren<Animator>();
                if (enemyAnim != null)
                {
                    AnimatorStateInfo stateInfo = enemyAnim.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.IsName("Attack") || stateInfo.IsTag("Attack"))
                    {
                        OnPerfectDodgeSuccess(enemyEntity);
                        return; 
                    }
                }
            }
        }
    }

    private IEnumerator PerfectDodgeWindowActive()
    {
        // Vẫn giữ lại I-frames mặc định của lướt (0.4s đầu tiên) để né các đòn cơ bản
        if (hurtbox != null) hurtbox.isPerfectDodging = true;
        yield return new WaitForSeconds(perfectDodgeWindow); 
        if (hurtbox != null) hurtbox.isPerfectDodging = false;
    }

    private IEnumerator PerformDash(Vector2 direction, bool isBackdash)
    {
        PlayerState previousState = currentState;
        currentState = PlayerState.Dashing;

        DisableHitbox();

        if (isBackdash && previousState == PlayerState.Grounded) anim.SetTrigger("Backdash"); 
        else anim.SetTrigger("Dash"); 

        perfectDodgeTriggered = false; // [FIX #3]: Reset cờ khi bắt đầu lướt mới

        TryProactivePerfectDodge();
        StartCoroutine(PerfectDodgeWindowActive());

        if (currentDashCharges == baseData.maxDashes) dashRechargeTimer = 0;
        if (!isPerfectDodge) currentDashCharges--;
        
        if (direction.y > 0)
        {
            jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
            anim.SetBool("isDashingUpward", true);
        }

        if (previousState == PlayerState.Airborne || previousState == PlayerState.DashStalling || (previousState == PlayerState.Grounded && direction.y > 0)) 
            dashesUsedInAir++;

        canAirAttack = true; 
        
        float originalDamping = rb.linearDamping;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        
        rb.linearVelocity = direction * baseData.dashForce;

        yield return new WaitForSeconds(baseData.dashTime); 

        if (direction.y != 0) 
        {
            currentState = PlayerState.DashStalling;
            rb.linearVelocity = rb.linearVelocity * 0.3f; 
            yield return new WaitForSeconds(baseData.dashStallTime); 
            currentState = PlayerState.Airborne;
        }
        else 
        {
            if (previousState == PlayerState.Grounded) BecomeGrounded();
            else currentState = PlayerState.Airborne;
        }

        rb.gravityScale = originalGravity; 
        rb.linearDamping = originalDamping;
        isPerfectDodge = false; 

        if (direction.y == 0) 
        {
            rb.linearVelocity = new Vector2(direction.x * currentMoveSpeed, rb.linearVelocity.y);
        }

        anim.SetBool("isDashingUpward", false);
    }

    public void OnPerfectDodgeSuccess(BaseEntity attacker)
    {
        if (isPhasingThrough) return;
        if (perfectDodgeTriggered) return; // [FIX #3]: Chặn đúp trigger

        if (perfectDodgeTimer <= 0f)
        {
            perfectDodgeTriggered = true; // [FIX #3]: Đánh dấu đã trigger

            Debug.Log("<color=cyan>PERFECT DODGE! NGƯNG ĐỌNG THỜI GIAN!</color>");
            if (TimeAnomalyManager.Instance != null) 
                TimeAnomalyManager.Instance.TriggerPerfectDodge();
            
            perfectDodgeTimer = perfectDodgeCooldown; 

            EnemyController enemy = attacker as EnemyController;
            if (enemy != null)
            {
                enemy.CancelCurrentAttackHitbox();
            }

            if (attacker != null)
            {
                // Truyền thẳng attacker (vốn đã là BaseEntity) vào sổ đen
                StartCoroutine(IgnoreSpecificAttackerRoutine(attacker, 2.2f));
                StartCoroutine(PhaseThroughSpecificEntity(attacker.gameObject));
            }
            else 
            {
                StartCoroutine(ResetPhasingTimer(0.4f)); 
            }

            if (currentState == PlayerState.Dashing)
            {
                rb.linearVelocity = currentDashDirection * baseData.dashForce;
            }
        }
        else
        {
            Debug.Log("<color=orange>Perfect Dodge đang CD! Chỉ hồi 1 Dash Charge.</color>");
            if (currentDashCharges < baseData.maxDashes) currentDashCharges++;
        }
    }

    // [MỚI]: Hàm ghi nhớ kẻ địch bị né để chặn sát thương
    private IEnumerator IgnoreSpecificAttackerRoutine(BaseEntity enemyEntity, float duration)
    {
        if (enemyEntity == null) yield break;

        if (!ignoredAttackers.Contains(enemyEntity)) ignoredAttackers.Add(enemyEntity);
        
        yield return new WaitForSeconds(duration);
        
        if (ignoredAttackers.Contains(enemyEntity)) ignoredAttackers.Remove(enemyEntity);
    }

    private IEnumerator PhaseThroughSpecificEntity(GameObject enemyObj)
    {
        isPhasingThrough = true; 
        Collider2D playerCol = GetComponent<Collider2D>();
        Collider2D[] enemyCols = enemyObj.GetComponentsInChildren<Collider2D>();

        if (playerCol != null && enemyCols.Length > 0)
        {
            foreach (var col in enemyCols) Physics2D.IgnoreCollision(playerCol, col, true);
        }

        float timer = 2.2f;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        if (playerCol != null && enemyCols.Length > 0)
        {
            foreach (var col in enemyCols) 
                if (col != null) Physics2D.IgnoreCollision(playerCol, col, false); // [FIX #7]: Check Null trước khi khôi phục
        }

        isPhasingThrough = false;
    }

    private IEnumerator ResetPhasingTimer(float duration)
    {
        isPhasingThrough = true;
        yield return new WaitForSeconds(duration);
        isPhasingThrough = false;
    }

    public override void ApplyDamage(DamageInfo info)
    {
        bool isDodging = (hurtbox != null && hurtbox.isPerfectDodging);
        if (currentHealth <= 0 || isDodging) return;

        // Truy ngược từ info.attacker (có thể là Hitbox) lên BaseEntity gốc
        if (info.attacker != null)
        {
            BaseEntity attackingEntity = info.attacker.GetComponentInParent<BaseEntity>();
            
            // Nếu kẻ đang đánh mình nằm trong sổ đen -> Vô hiệu hóa sát thương!
            if (attackingEntity != null && ignoredAttackers.Contains(attackingEntity))
            {
                Debug.Log("<color=green>Miễn nhiễm sát thương! Đòn này đến từ kẻ địch đang bị Time Stop!</color>");
                return;
            }
        }

        if (info.attacker != null)
        {
            float directionToAttacker = Mathf.Sign(info.attacker.transform.position.x - transform.position.x);
            float facingDirection = transform.localScale.x >= 0 ? 1f : -1f;
            if (directionToAttacker != facingDirection && directionToAttacker != 0)
            {
                Vector3 localScale = transform.localScale;
                localScale.x *= -1f;
                transform.localScale = localScale;
            }
        }

        Vector2 actualKnockback = info.knockbackForce;
        if (info.attacker != null)
        {
            float pushDirection = Mathf.Sign(transform.position.x - info.attacker.transform.position.x);
            actualKnockback = new Vector2(Mathf.Abs(info.knockbackForce.x) * pushDirection, info.knockbackForce.y);
        }

        CancelAttack();
        CancelDash();

        base.ApplyDamage(info); 
        
        if (currentHealth > 0)
        {
            anim.SetTrigger("Hurt");
            CancelAttack();
            CancelDash();
            
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(actualKnockback, ForceMode2D.Impulse);
            currentState = PlayerState.Airborne; 
        }
    }

    private void ExecuteAttack()
    {
        lastAttackInputTime = -10f; 
        CancelAttack(); 
        attackCoroutine = StartCoroutine(PerformAttack());
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true; 
        comboStep++;
        if (comboStep > attackDurations.Length) comboStep = 1; 

        currentAttackDuration = attackDurations[comboStep - 1];

        anim.SetBool("isAttacking", true);
        anim.SetInteger("comboStep", comboStep);
        anim.SetTrigger("Attack");
        
        lastAttackTime = Time.time;

        if (currentState == PlayerState.Airborne) 
        {
            rb.gravityScale = originalGravity * 0.2f; 
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -1f, 0.05f)); 
        }

        yield return new WaitForSeconds(currentAttackDuration);
        
        isAttacking = false;
        anim.SetBool("isAttacking", false);
        rb.gravityScale = originalGravity;
        DisableHitbox();
    }

    private void HandleComboReset()
    {
        if (Time.time - lastAttackTime > baseData.comboResetTime && comboStep > 0)
        {
            comboStep = 0;
            rb.gravityScale = originalGravity;
            CancelAttack(); 
        }
    }

    private void CancelAttack()
    {
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        isAttacking = false;
        anim.SetBool("isAttacking", false);
        DisableHitbox(); 
    }

    private void CancelDash()
    {
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        
        if (hurtbox != null)
        {
            hurtbox.isPerfectDodging = false;
        }
        
        isPerfectDodge = false;
        rb.gravityScale = originalGravity; 

        if (anim != null)
        {
            anim.ResetTrigger("Dash");
            anim.ResetTrigger("Backdash");
            anim.SetBool("isDashingUpward", false);
        }
    }

    public void EnableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(true); }
    public void DisableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(false); }

    protected override void Die()
    {
        // Reset sạch tất cả params trước
        anim.SetFloat("speed", 0f);
        anim.SetBool("isGrounded", false);
        anim.SetBool("isJumping", false);
        anim.SetBool("isFalling", false);
        anim.SetBool("isAttacking", false);
        anim.SetBool("isDashingUpward", false);
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Dash");
        anim.ResetTrigger("Backdash");
        anim.ResetTrigger("Hurt");

        // Sau đó mới set isDead để Animator chuyển state ngay lập tức
        anim.SetBool("isDead", true);
        
        if (hurtbox != null) hurtbox.gameObject.SetActive(false);

        gameObject.layer = LayerMask.NameToLayer("Default");
        
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        this.enabled = false; 
        Debug.Log("<color=red>GAME OVER!</color>");
    }

    private bool CanDash()
    {
        if (currentDashCharges <= 0) return false;
        if (verticalInput > 0 && jumpsLeft <= 0) return false;
        if (currentState == PlayerState.Grounded) return true;
        if (dashesUsedInAir >= baseData.maxAirDashes) return false;
        if (dashesUsedInAir == 1 && !dashResetByJump && !canAirAttack) return false;
        return true;
    }

    private void HandleDashRecharge()
    {
        if (currentDashCharges < baseData.maxDashes)
        {
            dashRechargeTimer += Time.deltaTime;
            if (dashRechargeTimer >= baseData.dashRechargeTime)
            {
                currentDashCharges = baseData.maxDashes; 
                dashRechargeTimer = 0;
            }
        }
    }

    private void Jump()
    {
        lastJumpTime = Time.time;
        jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); 
        rb.linearVelocity += Vector2.up * baseData.jumpForce;
        canAirAttack = true; 
        rb.gravityScale = originalGravity;
        currentState = PlayerState.Airborne;
    }

    private void HandleFastFall()
    {
        if ((currentState == PlayerState.Airborne || currentState == PlayerState.DashStalling) && Input.GetKeyDown(KeyCode.S))
        {
            float timeSinceLastS = Time.time - lastSKeyPressTime;
            if (timeSinceLastS <= baseData.doubleTapThreshold)
            {
                if (currentState == PlayerState.DashStalling)
                {
                    if (dashCoroutine != null) StopCoroutine(dashCoroutine);
                    rb.gravityScale = originalGravity;
                    currentState = PlayerState.Airborne;
                }
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -baseData.fastFallSpeed);
            }
            lastSKeyPressTime = Time.time;
        }
    }

    private void BecomeGrounded()
    {
        currentState = PlayerState.Grounded;
        jumpsLeft = baseData.maxJumps;
        canAirAttack = true;
        dashesUsedInAir = 0; 
        dashResetByJump = false;
        rb.gravityScale = originalGravity;
    }

    private void Flip()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;
        if (horizontalInput > 0 && transform.localScale.x < 0 || horizontalInput < 0 && transform.localScale.x > 0)
        {
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private void UpdateAnimations()
    {
        float visualSpeed = isAttacking ? 0 : Mathf.Abs(horizontalInput);
        anim.SetFloat("speed", visualSpeed);

        bool grounded = currentState == PlayerState.Grounded;
        
        // isJumping chỉ true khi player CHỦ ĐỘNG nhảy
        // dùng lastJumpTime thay vì velocity để tránh false positive
        bool jumping = !grounded 
                    && currentState == PlayerState.Airborne 
                    && (Time.time - lastJumpTime) < 0.5f  // ← chỉ true trong 0.5s sau khi nhảy
                    && rb.linearVelocity.y > 0.1f;
                    
        bool falling = !grounded 
                    && currentState == PlayerState.Airborne 
                    && rb.linearVelocity.y < -0.1f;

        anim.SetBool("isGrounded", grounded);
        anim.SetBool("isJumping",  jumping);
        anim.SetBool("isFalling",  falling);
    }

    private void CheckGrounded()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;
        if (Time.time - lastJumpTime <= 0.15f) return;

        Collider2D col = GetComponent<Collider2D>();

        // Đặt box BÊN DƯỚI chân, không phải bên trong collider
        Vector2 feetPos = new Vector2(col.bounds.center.x, col.bounds.min.y - 0.05f);
        Vector2 boxSize = new Vector2(col.bounds.size.x * 0.7f, 0.15f);

        bool isGroundedNow = Physics2D.OverlapBox(feetPos, boxSize, 0f, groundLayer) != null;

        if (isGroundedNow)
        {
            if (currentState == PlayerState.Airborne) BecomeGrounded();
        }
        else
        {
            if (currentState == PlayerState.Grounded)
                currentState = PlayerState.Airborne;
        }
    }

    public float GetCurrentMeleeDamage(out bool isCrit)
    {
        isCrit = false;
        
        int currentComboIndex = Mathf.Clamp(comboStep - 1, 0, comboDamageMultipliers.Length - 1);
        float multiplier = comboDamageMultipliers[currentComboIndex];
        
        float finalDamage = Attack * multiplier;

        if (UnityEngine.Random.Range(0f, 100f) <= CritRate)
        {
            finalDamage *= baseData.critDamageMultiplier; 
            isCrit = true;
        }

        return finalDamage; 
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 3.5f);
    }

    public void ForceGroundedState()
    {
        currentState = PlayerState.Grounded;
        jumpsLeft = baseData.maxJumps;
        canAirAttack = true;
        dashesUsedInAir = 0;
        dashResetByJump = false;
        rb.gravityScale = originalGravity;
    }

    private void HandleBossDetection()
    {
        // Quét 1 vòng tròn quanh Player để tìm Layer của Boss
        Collider2D hit = Physics2D.OverlapCircle(transform.position, bossDetectionRadius, bossLayer);
        
        if (hit != null)
        {
            // Nếu trúng, lấy thông tin Boss
            BaseEntity bossEntity = hit.GetComponentInParent<BaseEntity>();
            
            // Nếu là Boss mới (khác con cũ đang lưu) và nó còn sống -> Hiện UI
            if (bossEntity != null && activeBoss != bossEntity && bossEntity.currentHealth > 0)
            {
                activeBoss = bossEntity;
                if (BossUIManager.Instance != null) BossUIManager.Instance.SetupBoss(activeBoss);
            }
        }
        else if (activeBoss != null)
        {
            // Nếu quét không thấy ai mà trước đó đang có Boss -> Đã đi ra xa -> Ẩn UI
            activeBoss = null;
            if (BossUIManager.Instance != null) BossUIManager.Instance.HideBossUI();
        }
    }

    // ==========================================
    // LOGIC KỸ NĂNG HỖ TRỢ (BÙA)
    // ==========================================
    private void HandleSupportSkill()
    {
        if (equippedSupportSkill == null) return;

        // 1. Nạp số lượng đạn khi mới lắp bùa vào
        if (!isSupportSkillInitialized)
        {
            currentSupportSkillUses = equippedSupportSkill.maxUses;
            isSupportSkillInitialized = true;
        }

        // 2. Trừ thời gian hồi chiêu
        if (supportSkillCDTimer > 0)
        {
            supportSkillCDTimer -= Time.deltaTime;
        }

        // 3. Cập nhật UI liên tục
        if (SupportSkillUI.Instance != null)
        {
            SupportSkillUI.Instance.UpdateUI(equippedSupportSkill, supportSkillCDTimer, currentSupportSkillUses);
        }

        // 4. Lắng nghe phím E để xuất chiêu
        if (Input.GetKeyDown(KeyCode.E))
        {
            UseSupportSkill();
        }
    }

    private Transform FindNearestEnemy(float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.transform.root == this.transform) continue; // Bỏ qua bản thân

            BaseEntity enemy = hit.GetComponentInParent<BaseEntity>();
            // Kiểm tra xem có phải là Quái và còn sống không
            if (enemy != null && enemy.currentHealth > 0 && enemy.CompareTag("Enemy"))
            {
                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = enemy.transform;
                }
            }
        }
        return nearest;
    }

    private void UseSupportSkill()
    {
        if (equippedSupportSkill == null || equippedSupportSkill.skillPrefab == null) return;
        if (supportSkillCDTimer > 0) return; 
        if (currentSupportSkillUses <= 0) return; 

        supportSkillCDTimer = equippedSupportSkill.skillCooldown;
        currentSupportSkillUses--;

        anim.SetTrigger("Attack"); 
        
        float finalDamage = Attack * equippedSupportSkill.damageMultiplier;
        bool isCrit = false;

        if (UnityEngine.Random.Range(0f, 100f) <= CritRate)
        {
            finalDamage *= baseData.critDamageMultiplier; 
            isCrit = true;
        }

        float facingDir = transform.localScale.x >= 0 ? 1f : -1f;
        Vector3 spawnPos = transform.position + new Vector3(facingDir * 0.8f, 0.5f, 0); // Mặc định ở trước mặt
        
        Transform targetEnemy = FindNearestEnemy(15f); 
        
        // [QUAN TRỌNG]: Nhận diện xem đây là Đạn bay (Fire Ball) hay Đánh thẳng (Sét)
        bool isProjectile = equippedSupportSkill.skillPrefab.GetComponent<Projectile>() != null;

        if (targetEnemy != null)
        {
            // Xoay mặt Player về phía kẻ địch
            float dirToEnemy = Mathf.Sign(targetEnemy.position.x - transform.position.x);
            if (dirToEnemy != 0 && dirToEnemy != facingDir)
            {
                facingDir = dirToEnemy;
                Vector3 playerScale = transform.localScale;
                playerScale.x = Mathf.Abs(playerScale.x) * facingDir;
                transform.localScale = playerScale;
            }

            if (isProjectile)
            {
                // NẾU LÀ ĐẠN LỬA: Sinh ra trước mặt
                spawnPos = transform.position + new Vector3(facingDir * 0.8f, 0.5f, 0);
            }
            else
            {
                // NẾU LÀ SÉT: Đánh thẳng vào chân kẻ địch
                Collider2D col = targetEnemy.GetComponent<Collider2D>();
                float bottomY = col != null ? col.bounds.min.y : targetEnemy.position.y;
                spawnPos = new Vector3(targetEnemy.position.x, bottomY + 0.5f, targetEnemy.position.z);
            }
        }

        GameObject vfx = Instantiate(equippedSupportSkill.skillPrefab, spawnPos, Quaternion.identity);

        UniversalHitbox hb = vfx.GetComponent<UniversalHitbox>();
        if (hb != null)
        {
            hb.owner = this.gameObject;
            hb.damageOverride = finalDamage; 
            hb.isCriticalOverride = isCrit; 
        }

        // Nếu là Đạn Lửa và có mục tiêu -> Gán mục tiêu để nó bay theo
        if (isProjectile && targetEnemy != null)
        {
            Projectile proj = vfx.GetComponent<Projectile>();
            if (proj != null) proj.SetTarget(targetEnemy);
        }

        if (currentSupportSkillUses <= 0)
        {
            Debug.Log("<color=yellow>Bùa đã hết số lần sử dụng! Tự động hủy.</color>");
            equippedSupportSkill = null; 
            
            if (SupportSkillUI.Instance != null) SupportSkillUI.Instance.UpdateUI(null, 0, 0);
            if (InventoryManager.Instance != null) InventoryManager.Instance.RemoveBrokenEquipment(ItemType.SupportSkill);
        }
    }

    // Hàm này để hệ thống Inventory/Trang bị gọi khi bạn nhặt hoặc mặc bùa mới vào
    public void EquipSupportSkill(ItemSO newSkill)
    {
        equippedSupportSkill = newSkill;
        isSupportSkillInitialized = false; // Ép nạp lại số lượng đạn theo bùa mới
        supportSkillCDTimer = 0f;
    }
}