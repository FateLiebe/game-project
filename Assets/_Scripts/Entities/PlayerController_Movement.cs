using UnityEngine;
using System.Collections;

public partial class PlayerController
{
    #region INPUT & MOVEMENT
    // ==========================================

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

        anim.SetTrigger("Jump");
        playerAudio?.NotifyJump(); // [AUDIO]
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
        //anim.SetBool("isJumping",  jumping);
        anim.SetBool("isFalling",  falling);
    }

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

    #endregion

    // ==========================================
}
