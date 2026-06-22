using UnityEngine;
using System.Collections;

/// <summary>
/// Quản lý phần di chuyển (Movement), nhảy (Jump), lướt (Dash), và nhận diện mặt đất (Grounded) của Player.
/// </summary>
public partial class PlayerController
{
    #region INPUT & MOVEMENT
    // ==========================================

    /// <summary>
    /// Nhận diện phím bấm và chặn tương tác khi đang phản đòn (Counter) hoặc lướt (Dash).
    /// </summary>
    private void HandleInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (counterCooldownTimer > 0f) counterCooldownTimer -= Time.deltaTime;

        bool counterInput = Input.GetKeyDown(KeyCode.F) || uiCounterRequested;
        uiCounterRequested = false; // Đặt lại cờ sau khi đã ghi nhận

        if (counterInput && counterCooldownTimer <= 0f)
        {
            if (currentState == PlayerState.Grounded || currentState == PlayerState.Airborne)
            {
                if (!isAttacking && currentState != PlayerState.Dashing && currentState != PlayerState.Countering)
                {
                    ExecuteCounterStance();
                    return; // Chặn các input khác khi đang counter
                }
            }
        }

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

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
        {
            lastAttackInputTime = Time.time;
        }

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1) || uiDashRequested))
        {
            uiDashRequested = false;
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

    #endregion

    // ==========================================
    #region MOVEMENT LOGIC EXTENSIONS
    // ==========================================

    private bool CanDash()
    {
        if (currentDashCharges <= 0) return false;
        if (verticalInput > 0 && jumpsLeft <= 0) return false;
        if (currentState == PlayerState.Grounded) return true;
        if (dashesUsedInAir >= baseData.maxAirDashes) return false;
        if (dashesUsedInAir == 1 && !dashResetByJump && !canAirAttack) return false;
        return true;
    }

    /// <summary>
    /// Xử lý quá trình phục hồi (Recharge) số lần lướt (Dash Charge) theo thời gian.
    /// Giới hạn số charge tối đa theo baseData.maxDashes.
    /// </summary>
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

    /// <summary>
    /// Xử lý logic Nhảy (Jump). Áp dụng lực nhảy dựa trên thông số nhân vật và gọi sự kiện âm thanh.
    /// Cho phép hủy hoạt ảnh đang diễn ra để phản ứng ngay lập tức.
    /// </summary>
    private void Jump()
    {
        lastJumpTime = Time.time;
        jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); 
        rb.linearVelocity += Vector2.up * baseData.jumpForce;
        canAirAttack = true; 
        rb.gravityScale = originalGravity;
        currentState = PlayerState.Airborne;

        anim.SetTrigger("Jump");
        playerAudio?.NotifyJump();
    }

    /// <summary>
    /// Kích hoạt cơ chế rơi nhanh (Fast Fall) khi người chơi ấn mũi tên xuống trong lúc đang ở trên không.
    /// Tăng giới hạn tốc độ rơi để đáp đất nhanh hơn.
    /// </summary>
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

    /// <summary>
    /// Khôi phục trạng thái Mặt đất (Grounded). 
    /// Reset các cờ lướt trên không, số lần nhảy và dừng các hoạt ảnh hạ cánh.
    /// </summary>
    private void BecomeGrounded()
    {
        currentState = PlayerState.Grounded;
        jumpsLeft = baseData.maxJumps;
        canAirAttack = true;
        dashesUsedInAir = 0; 
        dashResetByJump = false;
        rb.gravityScale = originalGravity;
    }

    /// <summary>
    /// Lật sprite của nhân vật theo hướng di chuyển hiện tại.
    /// </summary>
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
        //anim.SetBool("isJumping",  jumping);
        anim.SetBool("isFalling",  falling);
    }

    /// <summary>
    /// Cơ chế kiểm tra mặt đất siêu an toàn bằng Raycast.
    /// Đòi hỏi nhân vật phải chạm đất liên tục 3 frame để chống hiện tượng giật lag bay trên không.
    /// </summary>
    /// <summary>
    /// Bắn Raycast xuống dưới chân để xác định xem Player có đang đứng trên mặt đất không.
    /// Cập nhật trạng thái Grounded/Airborne tương ứng.
    /// </summary>
    private void CheckGrounded()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;
        if (Time.time - lastJumpTime <= 0.25f) return;
        if (rb.linearVelocity.y > 0.05f)
        {
            // Đang bay lên → reset counter, không xét grounded
            groundedFrameCount = 0;
            return;
        }

        Collider2D col = GetComponent<Collider2D>();
        float bottom = col.bounds.min.y;
        float cx     = col.bounds.center.x;
        float hw     = col.bounds.size.x * 0.25f;

        bool hitL = Physics2D.Raycast(new Vector2(cx - hw, bottom), Vector2.down, 0.08f, groundLayer);
        bool hitC = Physics2D.Raycast(new Vector2(cx,       bottom), Vector2.down, 0.08f, groundLayer);
        bool hitR = Physics2D.Raycast(new Vector2(cx + hw,  bottom), Vector2.down, 0.08f, groundLayer);

        bool isGroundedNow = hitL || hitC || hitR;

        if (isGroundedNow)
        {
            notGroundedFrameCount = 0;
            groundedFrameCount++;

            // Chỉ BecomeGrounded sau khi ổn định 3 frame liên tiếp
            if (groundedFrameCount >= GROUND_CONFIRM_FRAMES && currentState == PlayerState.Airborne)
                BecomeGrounded();
        }
        else
        {
            groundedFrameCount = 0;
            notGroundedFrameCount++;

            // Chỉ rời Grounded sau khi không chạm đất 3 frame liên tiếp
            if (notGroundedFrameCount >= GROUND_CONFIRM_FRAMES && currentState == PlayerState.Grounded)
                currentState = PlayerState.Airborne;
        }
    }

    /// <summary>
    /// Trả về lượng sát thương cận chiến hiện tại, đã bao gồm các hệ số nhân từ Combo, Buff, và Chí mạng.
    /// </summary>
    /// <param name="isCrit">Trạng thái (out) báo hiệu cú đánh này có phải là chí mạng hay không</param>
    public float GetCurrentMeleeDamage(out bool isCrit)
    {
        isCrit = false;
        
        int currentComboIndex = Mathf.Clamp(comboStep - 1, 0, comboDamageMultipliers.Length - 1);
        float multiplier = comboDamageMultipliers[currentComboIndex];
        
        if (isCounterAttacking) 
        {
            // Khi ở trạng thái Counter Attack, sát thương đánh ra sẽ được cường hóa (Hệ số x2)
            multiplier = 2f;
        }
        
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

    #endregion

    // ==========================================
    #region UI BUTTON HANDLERS
    // ==========================================
    public void UIButton_Attack()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay) return;
        lastAttackInputTime = Time.time;
    }

    public void UIButton_Jump()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay) return;
        if (jumpsLeft > 0)
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
    }

    private bool uiDashRequested = false;
    public void UIButton_Dash()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay) return;
        uiDashRequested = true;
    }

    private bool uiCounterRequested = false;
    public void UIButton_CounterAttack()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay) return;
        uiCounterRequested = true;
    }
    #endregion
}
