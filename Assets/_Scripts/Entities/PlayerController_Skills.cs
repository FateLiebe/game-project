using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý Hệ thống Kỹ năng hỗ trợ (Bùa/Phép) và cơ chế Khóa mục tiêu (Lock Target) của Player.
/// Bao gồm quét quái tự động (Auto-aim) bằng OverlapCircle, ném đạn (Projectile) hoặc đánh thẳng mục tiêu (Sét).
/// Đồng thời phụ trách phát hiện Boss để báo hiệu cho UI.
/// </summary>
public partial class PlayerController
{
    #region BOSS DETECTION
    // ==========================================

    /// <summary>
    /// Radar quét Boss: Tự động chạy mỗi frame trong Update (thuộc PlayerController gốc).
    /// Bắn ra một vòng tròn quét tìm Layer Boss, nếu phát hiện sẽ bắn Event gọi BossUIManager hiện thanh máu.
    /// </summary>
    /// <summary>
    /// Kiểm tra xem người chơi có đang đứng trong khu vực đánh Boss hay không.
    /// Kích hoạt event hiển thị thanh máu Boss nếu có.
    /// </summary>
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
                OnBossDetected?.Invoke(activeBoss); // [PHASE 3]
            }
        }
        else if (activeBoss != null)
        {
            // Nếu quét không thấy ai mà trước đó đang có Boss -> Đã đi ra xa -> Ẩn UI
            activeBoss = null;
            OnBossLost?.Invoke(); // [PHASE 3]
        }
    }

    #endregion

    // ==========================================
    #region SUPPORT SKILLS
    // ==========================================
    /// <summary>
    /// Vòng lặp chính xử lý Bùa chú (Support Skill). 
    /// Tính toán thời gian hồi chiêu, tự động gửi dữ liệu cho UI và nhận nút E để xuất chiêu.
    /// </summary>
    /// <summary>
    /// Xử lý logic nhấn phím sử dụng Kỹ năng Hỗ trợ (Support Skill - Phím Q).
    /// Quản lý thời gian hồi chiêu và gửi sự kiện cập nhật lên UI.
    /// </summary>
    private void HandleSupportSkill()
    {
        if (equippedSupportSkill == null) return;

        // 0. Cập nhật lock target mỗi frame (đếm ngược timer, kiểm tra còn sống/tầm bắn)
        UpdateLockedTarget();

        // 1. Nạp số lượng đạn khi mới lắp bùa vào
        if (!isSupportSkillInitialized)
        {
            currentSupportSkillUses = equippedSupportSkill.maxUses;
            isSupportSkillInitialized = true;
        }

        // 2. Trừ thời gian hồi chiêu
        if (supportSkillCDTimer > 0) supportSkillCDTimer -= Time.deltaTime;

        // 3. Cập nhật UI liên tục — [PHASE 3] qua event, không gọi SupportSkillUI trực tiếp
        OnSupportSkillUpdated?.Invoke(equippedSupportSkill, supportSkillCDTimer, currentSupportSkillUses);

        // 4. Lắng nghe phím E để xuất chiêu
        if (Input.GetKeyDown(KeyCode.E)) UseSupportSkill();
    }

    // ==========================================
    // LOCK TARGET
    // ==========================================
    /// <summary>
    /// Tự động dò tìm và khóa mục tiêu gần nhất.
    /// Theo dõi và đếm ngược thời gian mất dấu (nếu kẻ địch ra khỏi tầm).
    /// </summary>
    private void UpdateLockedTarget()
    {
        if (lockedTarget == null) return;
        lockedTargetTimer -= Time.deltaTime;
        if (lockedTargetTimer <= 0f) { lockedTarget = null; return; }

        BaseEntity e = lockedTarget.GetComponentInParent<BaseEntity>();
        if (e == null || e.currentHealth <= 0) { lockedTarget = null; return; }

        // Hủy nếu ngoài tầm bắn
        float maxRange = (equippedSupportSkill != null && equippedSupportSkill.skillRange > 0f)
                         ? equippedSupportSkill.skillRange : 15f;
        if (Vector2.Distance(transform.position, lockedTarget.position) > maxRange)
            lockedTarget = null;
    }

    /// <summary>Gọi từ UniversalHitbox khi skill chạm trúng — làm mới timer nếu đúng locked target.</summary>
    public void RefreshLockTimerIfMatch(Transform hitTransform)
    {
        if (lockedTarget == null || hitTransform == null) return;
        // So sánh root transform để tránh nhầm child collider
        if (hitTransform.root == lockedTarget.root || hitTransform == lockedTarget)
            lockedTargetTimer = LOCK_DURATION;
    }

    // Backward compat — giữ nguyên để không phá code cũ
    public void RefreshLockTimer()
    {
        if (lockedTarget != null) lockedTargetTimer = LOCK_DURATION;
    }

    /// <summary>
    /// Quét một vùng tròn quanh Player để tìm kẻ địch hoặc Boss gần nhất.
    /// Dùng Physics2D.OverlapCircleNonAlloc để tối ưu hiệu năng (không tạo rác Garbage Collector).
    /// </summary>
    private Transform FindNearestEnemy(float radius)
    {
        // Nếu đang có locked target hợp lệ → dùng nó (kể cả quay hướng khác)
        UpdateLockedTarget();
        if (lockedTarget != null) return lockedTarget;

        float facingDir = transform.localScale.x >= 0 ? 1f : -1f;
        // Tối ưu hóa bộ nhớ: Tái sử dụng mảng tĩnh _enemyScanBuffer thay vì cấp phát (Allocate) mảng mới mỗi lần gọi hàm
        int count = Physics2D.OverlapCircle(transform.position, radius, ContactFilter2D.noFilter, _enemyScanBuffer);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _enemyScanBuffer[i];
            if (hit == null) continue;
            if (hit.transform.root == this.transform) continue;
            if (hit.GetComponentInParent<BreakableCrate>() != null) continue;

            BaseEntity enemy = hit.GetComponentInParent<BaseEntity>();
            if (enemy == null || enemy.currentHealth <= 0 || (!enemy.CompareTag("Enemy") && !enemy.CompareTag("Boss"))) continue;

            // Chỉ chọn enemy cùng hướng mặt
            float dx = enemy.transform.position.x - transform.position.x;
            if (Mathf.Sign(dx) != facingDir && Mathf.Abs(dx) > 0.5f) continue;

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy.transform; }
        }

        if (nearest != null) { lockedTarget = nearest; lockedTargetTimer = LOCK_DURATION; }
        return nearest;
    }

    /// <summary>
    /// Hàm Xử Lý Bắn Bùa.
    /// Tính toán sát thương tổng (Crit + Modifier). Nhận diện loại Kỹ năng: 
    /// - Nếu là Đạn Lửa (Projectile): Bắn từ tay Player bay tới địch.
    /// - Nếu là Sét: Giáng thẳng xuống đầu vị trí của kẻ địch.
    /// </summary>
    /// <summary>
    /// Triển khai sử dụng kỹ năng hỗ trợ: Trừ số lượt dùng, reset hồi chiêu, sinh ra VFX và định vị đòn đánh.
    /// Hỗ trợ cả cơ chế tự động tìm mục tiêu (Auto Lock-on).
    /// </summary>
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
        
        // Nhận diện xem kỹ năng hỗ trợ có phải dạng đạn bay (Projectile) hay tấn công định vị (Sét giáng xuống)
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

        GameObject vfx;
        if (ObjectPoolManager.Instance != null) vfx = ObjectPoolManager.Instance.Get(equippedSupportSkill.skillPrefab, spawnPos, Quaternion.identity);
        else vfx = Instantiate(equippedSupportSkill.skillPrefab, spawnPos, Quaternion.identity);

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
            ItemSO oldSkill = equippedSupportSkill;
            equippedSupportSkill = null; 
            
            OnSupportSkillUpdated?.Invoke(null, 0, 0); // [PHASE 3]
            if (InventoryManager.Instance != null) 
            {
                InventoryManager.Instance.RemoveBrokenEquipment(ItemType.SupportSkill);
                InventoryManager.Instance.AutoEquipSupportSkill(oldSkill);
            }
        }
    }

    // Hàm này để hệ thống Inventory/Trang bị gọi khi bạn nhặt hoặc mặc bùa mới vào
    public void EquipSupportSkill(ItemSO newSkill)
    {
        equippedSupportSkill = newSkill;
        isSupportSkillInitialized = false; // Ép nạp lại số lượng đạn theo bùa mới
        supportSkillCDTimer = 0f;
    }

    /// <summary>
    /// Buộc Kỹ năng hỗ trợ hiện tại lập tức bước vào thời gian hồi chiêu (Cooldown).
    /// </summary>
    /// <summary>
    /// Đưa kỹ năng hỗ trợ hiện tại vào trạng thái hồi chiêu (Cooldown).
    /// Thường được gọi sau khi Auto Equip (tự động gắn bùa mới) để tránh spam.
    /// </summary>
    public void PutSupportSkillOnCooldown()
    {
        if (equippedSupportSkill != null)
        {
            supportSkillCDTimer = equippedSupportSkill.skillCooldown;
        }
    }

    /// <summary>
    /// Tải lại kỹ năng hỗ trợ từ file Save (Ghi đè kỹ năng mặc định).
    /// Ngăn không cho cơ chế khởi tạo ghi đè lại bằng cờ isSupportSkillInitialized.
    /// </summary>
    public void LoadSupportSkillFromSave(ItemSO savedSkill, int savedUses)
    {
        equippedSupportSkill = savedSkill;
        currentSupportSkillUses = savedUses;
        isSupportSkillInitialized = true; // [Chặn bug ghi đè]
        supportSkillCDTimer = 0f;
        OnSupportSkillUpdated?.Invoke(equippedSupportSkill, 0f, currentSupportSkillUses); // [PHASE 3]
    }
    
    #endregion
}
