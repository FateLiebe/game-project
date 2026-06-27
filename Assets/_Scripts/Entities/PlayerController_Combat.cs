using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class PlayerController
{
    #region HEALTH & DAMAGE OVERRIDES
    /// <summary>
    /// Ghi đè hàm nhận sát thương của BaseEntity để tích hợp logic Phản Đòn (Counter) và Kháng sát thương (I-frames).
    /// </summary>
    public override void ApplyDamage(DamageInfo info)
    {
        // Kiểm tra phản đòn (Counter Attack)
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

    /// <summary>
    /// Gọi khi máu rơi xuống 0.
    /// Đưa nhân vật vào trạng thái bất động, xóa toàn bộ hoạt ảnh dư thừa.
    /// Lưu trữ tự động (Auto-save) ngay khoảnh khắc nhân vật chết.
    /// Giúp người chơi giữ lại kinh nghiệm và vật phẩm đã cày cuốc dù phải chơi lại tại điểm lưu cũ.
    /// </summary>
    protected override void Die()
    {
        // Reset sạch tất cả params trước
        anim.SetFloat("speed", 0f);
        anim.SetBool("isGrounded", false);
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

        // [FIX] Ép ẩn giao diện Boss ngay khi Player chết
        ForceHideBossUI();

        // Lưu trạng thái hiện tại (level, item) vào tệp trước khi nhân vật chết hẳn.
        // để khi Reload Save Point thì chỉ phục hồi vị trí nhưng giữ nguyên cấp độ/đồ.
        // [FIX BUG BẤT TỬ]: Lưu MaxHealth thay vì 0 vào file để tránh trường hợp
        // nhấn "Continue" từ Main Menu sẽ load player với 0 HP (gây miễn nhiễm sát thương do enemy kiểm tra currentHealth > 0).
        if (SaveDataManager.Instance != null && InventoryManager.Instance != null)
        {
            SaveDataManager.Instance.CollectDataFromGame(this, InventoryManager.Instance, false);
            // Ghi đè health = đầy để đảm bảo Continue luôn bắt đầu với HP đầy
            SaveDataManager.Instance.currentData.currentHealth = MaxHealth;
            SaveDataManager.Instance.SaveGameToFile();
        }

        if (GameManager.Instance != null) GameManager.Instance.GameOver();
        Debug.Log("<color=red>GAME OVER!</color>");
    }
    #endregion

    #region COMBAT CORE LOGIC (ATTACK)
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

    private void ExecuteAttack()
    {
        lastAttackInputTime = -10f; 
        CancelAttack(); 
        attackCoroutine = StartCoroutine(PerformAttack());
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
        
        // Xóa các cờ trạng thái khống chế (VD: khi ấn Dash để ngắt ngang hoạt ảnh chém)
        bool wasCounter = isCounterAttacking;
        isCounterAttacking = false;
        
        // Tránh ghi đè isPhasingThrough nếu đang là do Perfect Dodge sinh ra
        if (currentState == PlayerState.Attacking || currentState == PlayerState.Countering) 
            isPhasingThrough = false; 

        // [FIX TASK 1]: Thoát khỏi trạng thái kẹt Countering
        if (wasCounter && currentState == PlayerState.Countering)
        {
            if (groundedFrameCount > 0) ForceGroundedState();
            else currentState = PlayerState.Airborne;
        }

        anim.SetBool("isAttacking", false);
        DisableHitbox(); 
    }

    public void EnableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(true); }
    public void DisableHitbox() { if (attackHitbox != null) attackHitbox.SetActive(false); }

    /// <summary>
    /// Gọi bởi Checkpoint khi người chơi nhấn "Reload Save Point".
    /// Dọn dẹp trạng thái Dead, trả lại Layer vật lý, khôi phục trọng lực (nếu chết do rớt vực).
    /// </summary>
    public void Revive()
    {
        if (hurtbox != null) hurtbox.gameObject.SetActive(true);
        gameObject.layer = LayerMask.NameToLayer("Player"); // Trả lại Layer gốc
        anim.SetBool("isDead", false);
        isDead = false;
        this.enabled = true; // Nhận nút bấm trở lại
        
        lastCombatTime = -999f; // Đảm bảo thoái lui khỏi trạng thái chiến đấu khi hồi sinh

        lockedTarget = null; // Xóa bộ nhớ mục tiêu cũ tránh lỗi lock on vào vùng trống
        activeBoss = null;   // Bỏ qua Boss cũ đã bị hủy khi map load lại

        // Phục hồi lại tương tác vật lý (bị vô hiệu hóa nếu rơi xuống vực)
        if (rb != null) rb.gravityScale = originalGravity;

        anim.Rebind();
        anim.Update(0f);
    }
    #endregion

    #region COMBAT CORE LOGIC (COUNTER & DODGE)
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

    /// <summary>
    /// Chủ động quét xem người chơi có đang "lao đầu" vào đòn tấn công của quái vật hay không.
    /// Gọi ngay khi vừa bấm Dash. Nếu quái đang đánh mà mình Dash tới trúng khung hình -> Perfect Dodge ngay lập tức.
    /// </summary>
    private void TryProactivePerfectDodge()
    {
        if (perfectDodgeTimer > 0f) return; 

        int hitCount = Physics2D.OverlapCircle(transform.position, 3.5f, ContactFilter2D.noFilter, threatColliders);
        
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

    /// <summary>
    /// Xử lý logic khi kích hoạt Perfect Dodge thành công.
    /// - Gọi Ngưng đọng thời gian (Time Stop).
    /// - Cấp cho kẻ địch vừa bị né một "kim bài miễn tử" ngược: Player sẽ không bị nhận sát thương từ chính kẻ này trong vài giây.
    /// - Cho phép Player đi xuyên qua kẻ địch đó (Phasing) để lướt ra sau lưng chúng.
    /// </summary>
    public void OnPerfectDodgeSuccess(BaseEntity attacker)
    {
        if (isPhasingThrough) return;
        // Chặn gọi đúp hàm kích hoạt khi lướt trúng nhiều đạn/hitbox cùng một lúc
        if (perfectDodgeTriggered) return;

        if (perfectDodgeTimer <= 0f)
        {
            // Đánh dấu để ngăn các hitbox khác tiếp tục kích hoạt né hoàn hảo trong lượt lướt này
            perfectDodgeTriggered = true;

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
                StartCoroutine(IgnoreSpecificAttackerRoutine(attacker, 3f));
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
    #endregion

    #region COROUTINES
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
        playerAudio?.NotifyAttack(comboStep);
        
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

    /// <summary>
    /// Chuỗi combo chém phản công (Counter Attack). 
    /// Miễn nhiễm sát thương trong lúc chém và tung ra đòn chém mạnh nhất (Combo step 3).
    /// </summary>
    private IEnumerator PerformCounterAttackRoutine()
    {
        // KHÔNG đổi currentState thành Attacking để tránh kẹt logic di chuyển
        currentState = PlayerState.Countering; // Bổ sung để khóa cứng trạng thái
        isAttacking = true;
        isCounterAttacking = true; 
        
        comboStep = 3;
        currentAttackDuration = attackDurations[2]; 
        // Cập nhật lại thời điểm tấn công để tránh bị HandleComboReset hủy lệnh ngay lập tức
        lastAttackTime = Time.time;
        
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

    private IEnumerator CounterStanceRoutine()
    {
        yield return new WaitForSeconds(counterWindow);
        
        // Nếu không bị ai đánh trúng trong 0.2s -> Kết thúc stance
        if (currentState == PlayerState.Countering)
        {
            currentState = PlayerState.Grounded; // hoặc Airborne tuỳ vào chạm đất
        }
    }

    /// <summary>
    /// Luồng xử lý kỹ năng Lướt (Dash).
    /// Tạm thời tắt Hitbox, loại bỏ trọng lực, truyền lực đẩy mạnh về phía trước/sau.
    /// Đồng thời kích hoạt khung thời gian vô địch (I-Frames) ở đầu đòn lướt.
    /// </summary>
    private IEnumerator PerformDash(Vector2 direction, bool isBackdash)
    {
        PlayerState previousState = currentState;
        currentState = PlayerState.Dashing;

        DisableHitbox();

        if (isBackdash && previousState == PlayerState.Grounded) anim.SetTrigger("Backdash"); 
        else anim.SetTrigger("Dash"); 
        playerAudio?.NotifyDash();
        
        // Đặt lại cờ kích hoạt né hoàn hảo khi bắt đầu một chuỗi lướt mới
        perfectDodgeTriggered = false;

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

    private IEnumerator PerfectDodgeWindowActive()
    {
        // Vẫn giữ lại I-frames mặc định của lướt (0.4s đầu tiên) để né các đòn cơ bản
        if (hurtbox != null) hurtbox.isPerfectDodging = true;
        yield return new WaitForSeconds(perfectDodgeWindow); 
        if (hurtbox != null) hurtbox.isPerfectDodging = false;
    }

    /// <summary>
    /// Ghi nhớ kẻ địch hoặc đòn tấn công vừa bị né để không tính sát thương trong khoảng thời gian nhất định (Kim bài miễn tử).
    /// </summary>
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
                // Đảm bảo Collider vẫn tồn tại (kẻ địch chưa bị tiêu diệt) trước khi khôi phục va chạm
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
    #endregion

    #region HELPER METHODS
    /// <summary>
    /// Phát hiện xem có đòn tấn công nào đang lao tới từ phía sau lưng người chơi hay không.
    /// Thuật toán quét vùng hình tròn xung quanh, kiểm tra xem kẻ địch có hitbox đang bật hoặc đang bật hoạt ảnh Attack không.
    /// Dùng để kích hoạt "Né mù" (Blind Dodge) nếu người chơi lướt đúng lúc.
    /// </summary>
    private bool CheckIncomingAttackFromBehind(out float dirToThreat)
    {
        dirToThreat = 0f;
        float facingDir = transform.localScale.x >= 0 ? 1f : -1f;
        
        int hitCount = Physics2D.OverlapCircle(transform.position, 5f, ContactFilter2D.noFilter, threatColliders);
        
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
                    // So sánh trực tiếp Component thay vì chuỗi String để tăng tốc độ xử lý
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
    #endregion
}
