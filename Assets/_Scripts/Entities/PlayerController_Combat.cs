using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class PlayerController
{
    #region DODGE & PERFECT DODGE
    // ==========================================

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
        playerAudio?.NotifyDash(); // [AUDIO]

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
    
    #endregion

    // ==========================================
    #region HEALTH & DAMAGE (OVERRIDES)
    // ==========================================

    // Biến cờ đánh dấu đòn đánh là Counter Attack
    private bool isCounterAttacking = false;

    public override void ApplyDamage(DamageInfo info)
    {
        // [COUNTER ATTACK CHECK]
        if (currentState == PlayerState.Countering)
        {
            if (info.sourceHitbox != null && 
               (info.sourceHitbox.CompareTag("AttackHitbox") || info.sourceHitbox.CompareTag("RangedHitbox")))
            {
                TriggerCounterAttack();
                return; // Miễn nhiễm sát thương và chuyển sang phản đòn
            }
        }

        bool isDodging = (hurtbox != null && hurtbox.isPerfectDodging);
        if (currentHealth <= 0 || isDodging || isPhasingThrough) return; // Nếu đang trong quá trình phản đòn (isPhasingThrough) cũng miễn nhiễm

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
    
    #endregion

    // ==========================================
    #region COMBAT & ATTACK
    // ==========================================

    private void ExecuteCounterStance()
    {
        // Kích hoạt state Countering
        currentState = PlayerState.Countering;
        rb.linearVelocity = Vector2.zero; // Đứng yên
        
        anim.SetTrigger("Counter"); // Trigger hoạt ảnh giơ khiên
        
        // Trừ cooldown
        counterCooldownTimer = counterCooldown;
        
        if (counterCoroutine != null) StopCoroutine(counterCoroutine);
        counterCoroutine = StartCoroutine(CounterStanceRoutine());
    }

    private IEnumerator CounterStanceRoutine()
    {
        yield return new WaitForSeconds(counterWindow);
        
        // Nếu không bị ai đánh trúng trong 0.2s -> Kết thúc stance
        if (currentState == PlayerState.Countering)
        {
            currentState = PlayerState.Grounded; // hoặc Airborne tuỳ vào chạm đất
        }
    }

    public void TriggerCounterAttack()
    {
        if (counterCoroutine != null) StopCoroutine(counterCoroutine);
        
        // Chỉ phát âm thanh Counter, KHÔNG làm chậm thời gian
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCounterAttack();
        
        Debug.Log("<color=magenta>COUNTER ATTACK THÀNH CÔNG!</color>");
        
        // Bắt đầu chém Combo 3 (multiplier x2 sẽ tính trong GetCurrentMeleeDamage hoặc ép cứng)
        CancelAttack();
        attackCoroutine = StartCoroutine(PerformCounterAttackRoutine());
    }

    private IEnumerator PerformCounterAttackRoutine()
    {
        // KHÔNG đổi currentState thành Attacking để tránh kẹt logic di chuyển
        isAttacking = true;
        isCounterAttacking = true; 
        
        comboStep = 3;
        currentAttackDuration = attackDurations[2]; 
        lastAttackTime = Time.time; // [FIX LỖI]: Phải cập nhật thời gian để HandleComboReset không lập tức huỷ đòn đánh!
        
        anim.SetBool("isAttacking", true);
        anim.SetInteger("comboStep", comboStep);
        
        anim.Play("Attack_Combo_3", 0, 0f);
        
        EnableHitbox();
        playerAudio?.NotifyAttack(comboStep); 

        isPhasingThrough = true; 
        
        yield return new WaitForSeconds(currentAttackDuration);
        
        isPhasingThrough = false;
        isAttacking = false;
        isCounterAttacking = false; 
        anim.SetBool("isAttacking", false);
        DisableHitbox();
        
        comboStep = 0; 
        
        // Thoát khỏi trạng thái Countering: phân định rõ dưới đất hay trên không
        if (currentState == PlayerState.Countering)
        {
            if (groundedFrameCount > 0) ForceGroundedState();
            else currentState = PlayerState.Airborne;
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
        EnableHitbox(); // Bật hitbox cho đòn đánh thường
        playerAudio?.NotifyAttack(comboStep); // [AUDIO]
        
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
        
        // [FIX LỖI BẤT TỬ]: Phải reset các cờ này nếu bị ngắt ngang (ví dụ ấn Dash lúc đang chém)
        isCounterAttacking = false;
        
        // Tránh ghi đè isPhasingThrough nếu đang là do Perfect Dodge sinh ra
        if (currentState == PlayerState.Attacking || currentState == PlayerState.Countering) 
            isPhasingThrough = false; 

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
        if (GameManager.Instance != null) GameManager.Instance.GameOver();
        Debug.Log("<color=red>GAME OVER!</color>");
    }

    // Đánh thức nhân vật
    public void Revive()
    {
        if (hurtbox != null) hurtbox.gameObject.SetActive(true);
        gameObject.layer = LayerMask.NameToLayer("Player"); // Trả lại Layer gốc
        anim.SetBool("isDead", false);
        isDead = false;
        this.enabled = true; // Nhận nút bấm trở lại

        anim.Rebind();
        anim.Update(0f);
    }
    
    #endregion

    // ==========================================
}
