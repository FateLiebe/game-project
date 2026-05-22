using UnityEngine;
using System.Collections;

public class PlayerController : BaseEntity
{
    public enum PlayerState { Grounded, Airborne, Dashing, DashStalling, Attacking, Blocking }

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
    [SerializeField] private float parryWindow = 0.2f; 
    [SerializeField] private float blockDamageReduction = 0.2f; 
    [SerializeField] private float perfectDodgeCooldown = 15f;
    
    
    private float perfectDodgeTimer = 0f;
    private float blockStartTime;
    private float currentAttackDuration = 0f;
    private bool isAttacking = false;
    private Hurtbox hurtbox;

    [Header("Physics & Detection")]
    [SerializeField] private LayerMask groundLayer;
    
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
    
    // [FIX 6]: Cấp phát sẵn mảng quét va chạm để chống rác bộ nhớ (GC Spike)
    private Collider2D[] threatColliders = new Collider2D[20];

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
        HandleInput();
        HandleComboReset();
        Flip();
        UpdateAnimations();
        CheckGrounded();

        if (perfectDodgeTimer > 0)
        {
            perfectDodgeTimer -= Time.deltaTime;
        }
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateDodgeCD(perfectDodgeTimer, perfectDodgeCooldown);
        }
    }

  private void FixedUpdate()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;

        float targetSpeed = horizontalInput * currentMoveSpeed;
        if (isAttacking) targetSpeed *= attackMovementMultiplier;

        // Trả lại di chuyển nguyên bản, để Unity tự xử lý trượt dốc bằng ZeroFriction
        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }

    private void HandleInput()
    {
        if (currentState == PlayerState.Blocking) 
        {
            horizontalInput = 0; 
            if (Input.GetKeyUp(KeyCode.R))
            {
                currentState = PlayerState.Grounded;
                anim.SetBool("isBlocking", false);
            }
            return; 
        }

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
                    // [FIX 5]: Chống bug localScale.x = 0 làm nhân vật kẹt cứng
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
                    // [FIX 5]: Chống lỗi tương tự cho lướt không hướng
                    if (dashDir == Vector2.zero) dashDir = new Vector2(transform.localScale.x >= 0 ? 1f : -1f, 0);
                    isBackdash = (dashDir.x * transform.localScale.x) < 0;
                }

                currentDashDirection = dashDir; 
                dashCoroutine = StartCoroutine(PerformDash(dashDir, isBackdash));
                return; 
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && currentState == PlayerState.Grounded && !isAttacking)
        {
            currentState = PlayerState.Blocking;
            blockStartTime = Time.time; 
            anim.SetBool("isBlocking", true); 
            rb.linearVelocity = Vector2.zero; 
            return;
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
        
        // [FIX 6]: Dùng OverlapCircleNonAlloc để không sinh rác bộ nhớ
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
                    bool isHitbox = col.name.ToLower().Contains("hitbox") && col.enabled;
                    
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

    private IEnumerator PerformDash(Vector2 direction, bool isBackdash)
    {
        PlayerState previousState = currentState;
        currentState = PlayerState.Dashing;

        DisableHitbox();

        if (isBackdash && previousState == PlayerState.Grounded) anim.SetTrigger("Backdash"); 
        else anim.SetTrigger("Dash"); 

        if (hurtbox != null) hurtbox.isPerfectDodging = true; 

        if (currentDashCharges == baseData.maxDashes) dashRechargeTimer = 0;
        if (!isPerfectDodge) currentDashCharges--;
        
        if (direction.y > 0)
        {
            // [FIX 3]: An toàn không bao giờ cho nhảy về số âm
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

        yield return new WaitForSeconds(0.2f); 
        if (hurtbox != null) hurtbox.isPerfectDodging = false; 

        float remainingDashTime = Mathf.Max(0, baseData.dashTime - 0.2f);
        yield return new WaitForSeconds(remainingDashTime);

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
        // [FIX 7]: Chặn Multi-Hit kích hoạt lỗi nhiều Coroutine PhaseThrough
        if (isPhasingThrough) return;

        if (perfectDodgeTimer <= 0f)
        {
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
                if (col != null) Physics2D.IgnoreCollision(playerCol, col, false);
        }

        isPhasingThrough = false;
    }

    private IEnumerator ResetPhasingTimer(float duration)
    {
        isPhasingThrough = true;
        yield return new WaitForSeconds(duration);
        isPhasingThrough = false;
    }

    // [FIX 1]: Gộp và loại bỏ hoàn toàn Duplicate ApplyDamage
    public override void ApplyDamage(DamageInfo info)
    {
        if (isPhasingThrough) return;

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

        if (currentState == PlayerState.Blocking && info.attacker != null)
        {
            float attackerDirection = info.attacker.transform.position.x - transform.position.x;
            float facingDirection = transform.localScale.x;

            if (attackerDirection * facingDirection > 0)
            {
                if (Time.time - blockStartTime <= parryWindow)
                {
                    Debug.Log("<color=yellow>PARRY THÀNH CÔNG!</color>");
                    return; 
                }
                else
                {
                    Debug.Log("<color=cyan>BLOCK! Đỡ thành công một phần!</color>");
                    info.damage *= blockDamageReduction; 
                    actualKnockback *= 0.5f; 
                    
                    base.ApplyDamage(info);
                    if (currentHealth > 0)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.AddForce(actualKnockback, ForceMode2D.Impulse);
                    }
                    return;
                }
            }
            else
            {
                Debug.Log("<color=red>Bị chém lén sau lưng! Vỡ Block!</color>");
                currentState = PlayerState.Grounded;
                anim.SetBool("isBlocking", false);
            }
        }

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
        
        // [FIX 2]: Tháo mác miễn nhiễm sát thương để không bị bất tử vĩnh viễn khi dính đòn
        if (hurtbox != null)
        {
            hurtbox.isPerfectDodging = false;
            // Dựa theo logic được cấp, đảm bảo reset biến invincible nếu có
            // Lưu ý: Đảm bảo Hurtbox.cs của bạn có khai báo public bool isInvincible;
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
        anim.SetBool("isDead", true);
        GetComponent<Collider2D>().enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0;
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
        // [FIX 3]: Chống âm số lượt nhảy
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

        bool jumping = currentState == PlayerState.Airborne && rb.linearVelocity.y > 0.1f;
        bool falling = currentState == PlayerState.Airborne && rb.linearVelocity.y <= 0.1f;
        bool grounded = currentState == PlayerState.Grounded; 

        anim.SetBool("isJumping", jumping);
        anim.SetBool("isFalling", falling);
        anim.SetBool("isGrounded", grounded); 
    }

    private void CheckGrounded()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;

        // Bỏ qua check trong 0.1s đầu tiên sau khi nhảy để nhân vật kịp thoát khỏi mặt đất
        if (Time.time - lastJumpTime <= 0.1f) return;

        Collider2D col = GetComponent<Collider2D>();
        
        // 1. QUÉT CHẠM ĐẤT BẰNG HỘP TĨNH (Chống mù mặt dốc)
        // Tâm hộp quét: Nhích lên 0.1f so với gầm giày
        Vector2 feetPos = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f);
        // Kích thước hộp: Rộng bằng 70% nhân vật, cao 0.25f (Nó sẽ thò xuống dưới gầm giày một chút xíu)
        Vector2 boxSize = new Vector2(col.bounds.size.x * 0.7f, 0.25f);

        bool wasGrounded = currentState == PlayerState.Grounded; 
        bool isGroundedNow = Physics2D.OverlapBox(feetPos, boxSize, 0f, groundLayer) != null; 

        // 2. HÚT BÁM ĐỈNH DỐC (Chống văng lên trời khi chạy qua đỉnh)
        if (!isGroundedNow && wasGrounded)
        {
            // Bắn tia thẳng xuống dưới chân 0.5f để tìm mặt đất
            RaycastHit2D snapHit = Physics2D.Raycast(feetPos, Vector2.down, 0.5f, groundLayer);

            if (snapHit.collider != null)
            {
                isGroundedNow = true;
                // Tuyệt chiêu: Ép một vận tốc hướng xuống để nhân vật tự trượt dính vào mặt dốc 
                // nhờ lớp ma sát ZeroFriction, hoàn toàn không bị kẹt hay lún tường!
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -5f);
            }
        }

        // 3. CẬP NHẬT TRẠNG THÁI
        if (isGroundedNow)
        {
            if (currentState == PlayerState.Airborne) BecomeGrounded();
        }
        else
        {
            if (currentState == PlayerState.Grounded)
            {
                currentState = PlayerState.Airborne;
                jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
            }
        }
    }

    public float GetCurrentMeleeDamage(out bool isCrit)
    {
        isCrit = false;
        
        // 1. Tính sát thương dựa trên Step Combo hiện tại (comboStep bắt đầu từ 1 nên index mảng là comboStep - 1)
        int currentComboIndex = Mathf.Clamp(comboStep - 1, 0, comboDamageMultipliers.Length - 1);
        float multiplier = comboDamageMultipliers[currentComboIndex];
        
        float finalDamage = Attack * multiplier;

        // 2. Tính tỉ lệ bạo kích
        if (UnityEngine.Random.Range(0f, 100f) <= CritRate)
        {
            finalDamage *= baseData.critDamageMultiplier; // x1.7 sát thương
            isCrit = true;
        }

        return finalDamage; 
    }
}