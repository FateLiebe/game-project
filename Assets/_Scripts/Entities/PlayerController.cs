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
    
    // Cờ I-Frame: Chỉ bật sau khi Perfect Dodge THỰC SỰ NỔ để chặn Multi-hit
    private bool isPhasingThrough = false; 

    [Header("Combat System")]
    [SerializeField] private GameObject attackHitbox;
    [SerializeField] private float[] attackDurations = new float[] { 0.3f, 0.4f, 0.6f }; 
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
    private Vector2 currentDashDirection; // Lưu vết hướng lướt để bơm lại gia tốc sau va chạm
    private float lastAttackTime;
    private float lastAttackInputTime = -10f;
    private float lastSKeyPressTime;
    private float horizontalInput;
    private float verticalInput;
    private float originalGravity;


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
    }

    private void FixedUpdate()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;
        
        float targetSpeed = horizontalInput * currentMoveSpeed;

        if (isAttacking)
        {
            targetSpeed *= attackMovementMultiplier;
        }

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

                // XỬ LÝ LOGIC LƯỚT KHI ĐỨNG IM CÓ ĐỊCH SAU LƯNG
                if (horizontalInput == 0 && verticalInput == 0 && currentState == PlayerState.Grounded)
                {
                    float currentFacing = Mathf.Sign(transform.localScale.x);
                    float dirToThreat = 0f;

                    if (CheckIncomingAttackFromBehind(out dirToThreat))
                    {
                        if (perfectDodgeTimer <= 0f)
                        {
                            // ĐIỀU KIỆN 3: PD Sẵn sàng -> Lướt ngược chiều địch (hướng vào mặt nó)
                            dashDir = new Vector2(dirToThreat, 0);
                            isBackdash = true; 
                        }
                        else
                        {
                            // ĐIỀU KIỆN 1: PD Đang CD + địch đang tấn công sau lưng
                            // -> Flip quay mặt về phía địch NGAY LẬP TỨC, rồi backdash ra xa
                            // Sau khi flip: localScale.x = dirToThreat, dashDir = -dirToThreat = lùi lưng vào địch
                            // -> Animation Backdash mới đúng nghĩa: lưng quay về phía địch, mặt quay ra ngoài
                            Vector3 newScale = transform.localScale;
                            newScale.x = Mathf.Abs(newScale.x) * dirToThreat; // Flip về phía địch
                            transform.localScale = newScale;

                            dashDir = new Vector2(-dirToThreat, 0); // Dash ngược lại = lùi ra xa
                            isBackdash = true;
                        }
                    }
                    else
                    {
                        // ĐIỀU KIỆN 2: Địch ở sau nhưng KHÔNG tấn công -> Lùi đâm vào địch
                        dashDir = new Vector2(-currentFacing, 0);
                        isBackdash = true;
                    }
                }
                else
                {
                    // Lướt có phím điều hướng -> dùng hàm chuẩn
                    dashDir = CalculateDashDirection();
                    if (dashDir == Vector2.zero) dashDir = new Vector2(Mathf.Sign(transform.localScale.x), 0);
                    isBackdash = (dashDir.x * transform.localScale.x) < 0;
                }

                currentDashDirection = dashDir; // Ghi nhớ hướng lướt để dùng cho Tầng 2
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
            else if (rb.linearVelocity.y <= 0.1f || currentState == PlayerState.DashStalling)
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
        float facingDir = Mathf.Sign(transform.localScale.x);
        
        // Quét bán kính 5m xung quanh nhân vật
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 5f); 
        
        foreach (Collider2D col in colliders)
        {
            // Bỏ qua các collider thuộc về chính Player
            if (col.transform.root == this.transform || col.CompareTag("Player")) continue;

            // [FIX CHUẨN AAA]: Leo lên cây phả hệ để tóm lấy BaseEntity (Quái vật)
            // Không quan tâm collider hiện tại là Hitbox, tay, chân hay vũ khí
            BaseEntity enemyEntity = col.GetComponentInParent<BaseEntity>();

            // Nếu tìm thấy một BaseEntity (chắc chắn là quái vì đã loại trừ Player ở trên)
            if (enemyEntity != null)
            {
                // Lấy tọa độ của Entity gốc để tính hướng cho chuẩn xác
                float direction = Mathf.Sign(enemyEntity.transform.position.x - transform.position.x);

                // CHỈ KIỂM TRA NẾU KẺ ĐỊCH NẰM Ở SAU LƯNG
                if (direction != facingDir && direction != 0)
                {
                    // Dấu hiệu 1: Collider này đích thị là một Hitbox đang được bật
                    // (Chuyển ToLower để bắt được cả "HitBox", "hitbox", "AttackHitbox"...)
                    bool isHitbox = col.name.ToLower().Contains("hitbox") && col.enabled;
                    
                    // Dấu hiệu 2: Animator của thằng cha đang chạy state Tấn công
                    bool isPlayingAttackAnim = false;
                    Animator enemyAnim = enemyEntity.GetComponentInChildren<Animator>();
                    if (enemyAnim != null)
                    {
                        AnimatorStateInfo stateInfo = enemyAnim.GetCurrentAnimatorStateInfo(0);
                        isPlayingAttackAnim = stateInfo.IsName("Attack") || stateInfo.IsTag("Attack");
                    }

                    // CHỈ CẦN 1 TRONG 2 DẤU HIỆU LÀ XÁC NHẬN TẤN CÔNG
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

        // TẦNG 1: CHỈ CẤP QUYỀN CHỜ NÉ (Pre-Window). KHÔNG ĐI XUYÊN GÌ CẢ Ở ĐÂY.
        if (hurtbox != null) hurtbox.isPerfectDodging = true; 

        if (currentDashCharges == baseData.maxDashes) dashRechargeTimer = 0;
        if (!isPerfectDodge) currentDashCharges--;
        
        if (direction.y > 0)
        {
            jumpsLeft--;
            anim.SetBool("isDashingUpward", true);
        }

        if (previousState == PlayerState.Airborne || previousState == PlayerState.DashStalling || (previousState == PlayerState.Grounded && direction.y > 0)) 
            dashesUsedInAir++;

        canAirAttack = true; 
        
        float originalDamping = rb.linearDamping;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        
        rb.linearVelocity = direction * baseData.dashForce;

        // Cửa sổ vàng Perfect Dodge 0.2 Giây
        yield return new WaitForSeconds(0.2f); 
        if (hurtbox != null) hurtbox.isPerfectDodging = false; // Đóng Tầng 1

        // Quãng thời gian Dash còn lại
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

    // TẦNG 2: XÁC NHẬN THÀNH CÔNG (CONFIRM SUCCESS)
    public void OnPerfectDodgeSuccess(BaseEntity attacker)
    {
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

            // Xác nhận thành công -> Kích hoạt Đi Xuyên Cục Bộ (FIX LỖI 3 & 4)
            if (attacker != null)
            {
                StartCoroutine(PhaseThroughSpecificEntity(attacker.gameObject));
            }
            else 
            {
                // Né bẫy môi trường (không có attacker cụ thể)
                StartCoroutine(ResetPhasingTimer(0.4f)); 
            }

            // Bơm lại gia tốc lướt NGAY LẬP TỨC để triệt tiêu lực cản của Physics lúc va chạm
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

    // [KIẾN TRÚC AAA]: Chỉ bỏ qua va chạm với CÁC COLLIDER CỦA RIÊNG KẺ ĐỊCH NÀY
    private IEnumerator PhaseThroughSpecificEntity(GameObject enemyObj)
    {
        isPhasingThrough = true; // Bật cờ I-frame chống sát thương đè
        
        Collider2D playerCol = GetComponent<Collider2D>();
        Collider2D[] enemyCols = enemyObj.GetComponentsInChildren<Collider2D>();

        // Tắt va chạm CỤC BỘ
        if (playerCol != null && enemyCols.Length > 0)
        {
            foreach (var col in enemyCols) Physics2D.IgnoreCollision(playerCol, col, true);
        }

        // Duy trì đi xuyên cho đến khi hết Dash (hoặc Failsafe 0.4s)
        float timer = 2.2f;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        // Bật lại va chạm CỤC BỘ
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


    public override void ApplyDamage(DamageInfo info)
    {
        // QUAN TRỌNG: NẾU ĐANG ĐI XUYÊN (I-FRAME NỔ TỪ TẦNG 2), MIỄN NHIỄM MỌI SÁT THƯƠNG
        // Khóa sạch mọi lỗi Multi-hit từ các đòn đánh tồn tại quá lâu
        if (isPhasingThrough) return;

        if (info.attacker != null)
        {
            float directionToAttacker = Mathf.Sign(info.attacker.transform.position.x - transform.position.x);
            float facingDirection = Mathf.Sign(transform.localScale.x);
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
                    Debug.Log("<color=yellow>PARRY THÀNH CÔNG! Đỡ hoàn hảo!</color>");
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
            if (dashCoroutine != null) StopCoroutine(dashCoroutine);
            
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

    public void EnableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(true); }
    public void DisableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(false); }

    protected override void Die()
    {
        anim.SetBool("isDead", true);
        GetComponent<Collider2D>().enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0;
        this.enabled = false; 
        Debug.Log("<color=red>GAME OVER! Bạn đã nằm xuống.</color>");
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
        jumpsLeft--;
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

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    if (currentState == PlayerState.Airborne && rb.linearVelocity.y <= 0.1f) BecomeGrounded();
                    return;
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            if (currentState == PlayerState.Grounded)
            {
                currentState = PlayerState.Airborne;
                if (jumpsLeft == baseData.maxJumps) jumpsLeft--; 
            }
        }
    }

    private void CheckGrounded()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;

        Collider2D col = GetComponent<Collider2D>();
        Vector2 feetPos = new Vector2(col.bounds.center.x, col.bounds.min.y);

        bool isGroundedNow = Physics2D.OverlapCircle(feetPos, 0.15f, groundLayer);

        if (isGroundedNow)
        {
            if (currentState == PlayerState.Airborne && rb.linearVelocity.y <= 0.1f)
            {
                BecomeGrounded();
            }
        }
        else
        {
            if (currentState == PlayerState.Grounded)
            {
                currentState = PlayerState.Airborne;
                if (jumpsLeft == baseData.maxJumps) jumpsLeft--;
            }
        }
    }
}