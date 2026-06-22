using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Trí tuệ nhân tạo (AI) dành riêng cho Boss. Boss có khả năng bay lượn, xài khiên, nổi điên (Phase 2), và sử dụng 7 loại skill khác nhau dựa trên khoảng cách.
/// </summary>
public class BossController : EnemyBase
{
    [Header("--- RANGES & GIZMOS ---")]
    public float meleeRange = 3f;
    public float midRange = 7f;
    public float maxRange = 12f;

    [Header("--- SKILL SPECIFIC RANGES ---")]
    public float[] skillRanges = new float[7] { 8f, 8f, 15f, 99f, 4f, 15f, 3.5f };

    [Header("--- GLOBAL COOLDOWNS ---")]
    public float meleeAttackDelay = 1.5f;
    public float rangedAttackDelay = 2.5f;
    private float globalAttackTimer = 0f;

    [Header("--- BOSS AI TUNING ---")]
    public float preferredCombatDistance = 8f;
    public float combatDistanceBuffer = 1.4f;
    public float decisionInterval = 0.2f;
    public float repositionDuration = 0.45f;
    public float strafeDistance = 1.8f;
    public float panicBackOffDistance = 1.75f;

    [Header("--- FLIGHT HEIGHT CONTROL ---")]
    public float minFlightHeight = 2.5f;
    public float maxFlightHeight = 6.0f;
    public float verticalSpeed = 4.0f;
    public LayerMask groundLayer;

    [Header("--- DAMAGE FLIGHT REACTION ---")]
    public float damageToFlyUp = 30f;
    public float timeAtMaxHeight = 4.0f;

    [Header("--- HORIZONTAL MOVEMENT ---")]
    public float moveSpeed = 2.5f;

    [Header("--- BOSS PHASES ---")]
    public bool isPhase2 = false;

    [Header("--- BOSS DATA ---")]
    public BossDataSO bossData;

    // Boss HP = EnemyBase.MaxHealth × 8 (hoặc hệ số từ bossData)
    public override float MaxHealth => (enemyData != null ? enemyData.baseMaxHealth + ((currentLevel - 1) * enemyData.healthGrowth) : 150f + ((currentLevel - 1) * 30f)) * (bossData != null ? bossData.bossHealthMultiplier : 8f);

    [Header("--- BOSS SHIELD & BUFF ---")]
    //public float currentShield = 0f;
    public float maxShield = 0f;
    private float originalAttack;
    private float originalDefense;
    private Coroutine buffCoroutine;
    private Coroutine shieldCoroutine;

    private BossSkillManager skillManager;
    private Animator anim;
    private Transform playerTarget;
    private int facingDir = 0;
    private bool isActing = false;
    private int currentSelectedSkill = -1;

    private float currentTargetHeight;
    private float accumulatedDamage = 0f;
    private float timeSinceLastDamage = 0f;
    private float currentGroundY = 0f;
    private bool isOverGround = true;

    private float nextDecisionTime = 0f;
    private bool isInTransitionSkill = false;

    private Queue<int> recentSkills = new Queue<int>();
    private const int RECENT_SKILL_LIMIT = 3;

    protected override void Start()
    {
        base.Start();
        SyncLevelWithPlayer();

        anim = GetComponent<Animator>();
        skillManager = GetComponent<BossSkillManager>();

        if (rb != null) rb.gravityScale = 0f;
        
        // Vô hiệu hóa trọng lực của EnemyBase do Boss có cơ chế bay độc lập
        isFlying = true; 
        currentTargetHeight = minFlightHeight;
        
        // Bắt đầu vòng lặp tư duy hành vi của AI
        StartCoroutine(BehaviorTreeLoop());
    }

    /// <summary>
    /// Đồng bộ cấp độ và chỉ số của Boss dựa trên cấp độ hiện tại của Player.
    /// Hàm này xử lý linh động để đảm bảo Boss luôn mạnh hơn Player một khoảng cố định.
    /// </summary>
    public void SyncLevelWithPlayer()
    {
        // 1. Dò tìm Player và thiết lập mục tiêu
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTarget = p.transform;
            BaseEntity playerEntity = p.GetComponent<BaseEntity>();
            if (playerEntity != null && playerEntity.currentLevel > 0)
            {
                // Boss luôn ở cấp độ cao hơn Player 3 cấp
                this.currentLevel = playerEntity.currentLevel + 3;
            }
        }

        // 2. Tính toán chỉ số cơ bản dựa trên dữ liệu EnemyBase và mức tăng trưởng
        float enemyBaseATK = enemyData != null ? enemyData.baseAttack + ((currentLevel - 1) * enemyData.attackGrowth) : 12f + ((currentLevel - 1) * 4f);
        float enemyBaseDEF = enemyData != null ? enemyData.baseDefense + ((currentLevel - 1) * enemyData.defenseGrowth) : 3f  + ((currentLevel - 1) * 1f);

        // 3. Áp dụng hệ số nhân sức mạnh dành riêng cho Boss từ Data SO
        buffAttack  = enemyBaseATK * (bossData != null ? bossData.bossAttackBuffMultiplier : 0.8f);
        buffDefense = enemyBaseDEF * (bossData != null ? bossData.bossDefenseBuffMultiplier : 1.0f);

        // 4. Cập nhật lượng máu tối đa và bơm đầy máu khi Boss xuất hiện
        currentHealth = MaxHealth;

        // Lưu trữ chỉ số gốc để phục vụ cho các kỹ năng tự buff (Smack Buff)
        originalAttack  = Attack;
        originalDefense = Defense;
    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;

        if (globalAttackTimer > 0f)
        {
            globalAttackTimer -= Time.deltaTime * timeMultiplier;
        }

        timeSinceLastDamage += Time.deltaTime * timeMultiplier;

        if (Mathf.Approximately(currentTargetHeight, maxFlightHeight) && timeSinceLastDamage >= timeAtMaxHeight)
        {
            currentTargetHeight = minFlightHeight;
            accumulatedDamage = 0f;
        }
    }

    /// <summary>
    /// Vòng lặp ra quyết định của Behavior Tree. 
    /// Liên tục kiểm tra trạng thái và môi trường sau mỗi khoảng thời gian (decisionInterval) để quyết định hành động tiếp theo.
    /// </summary>
    private IEnumerator BehaviorTreeLoop()
    {
        yield return new WaitForSeconds(1.5f);

        while (!isDead)
        {
            if (playerTarget == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (!isActing && Time.time >= nextDecisionTime)
            {
                yield return StartCoroutine(EvaluateBehavior());
                nextDecisionTime = Time.time + decisionInterval;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Cây hành vi cốt lõi (Behavior Tree). 
    /// Quyết định chọn skill dựa vào khoảng cách tới người chơi, máu còn lại, và tránh xài trùng skill liên tiếp.
    /// </summary>
    private IEnumerator EvaluateBehavior()
    {
        isActing = true;

        if (playerTarget != null)
        {
            FaceTarget(playerTarget.position);
        }

        float dist = Vector2.Distance(transform.position, playerTarget.position);
        float hpPercent = (MaxHealth > 0f) ? Mathf.Clamp01(currentHealth / MaxHealth) : 1f;

        if (hpPercent <= 0.5f && !isPhase2)
        {
            isPhase2 = true;
            if (CanAttack(3))
            {
                isInTransitionSkill = true;
                globalAttackTimer = Mathf.Max(globalAttackTimer, rangedAttackDelay);
                yield return StartCoroutine(ExecuteSkillState(3, 1.5f));
                isInTransitionSkill = false;
            }
            isActing = false;
            yield break;
        }

        currentSelectedSkill = -1;

        if (dist <= panicBackOffDistance)
        {
            yield return StartCoroutine(TacticalMovementRoutine(forceBackOff: true));
            isActing = false;
            yield break;
        }

        if (globalAttackTimer <= 0f)
        {
            currentSelectedSkill = ChooseBestSkillByDistance(dist);
        }

        if (currentSelectedSkill != -1)
        {
            bool isMelee = IsMeleeSkill(currentSelectedSkill);
            globalAttackTimer = isMelee ? meleeAttackDelay : rangedAttackDelay;

            FaceTarget(playerTarget.position);
            yield return StartCoroutine(ExecuteSkillState(currentSelectedSkill, isMelee ? 1.05f : 1.2f));
        }
        else
        {
            yield return StartCoroutine(TacticalMovementRoutine(forceBackOff: false));
        }

        isActing = false;
    }

    /// <summary>
    /// Thu thập và chọn lọc kỹ năng tốt nhất dựa trên khoảng cách tới mục tiêu.
    /// Ưu tiên các đòn đánh gần (Melee) nếu mục tiêu ở gần, và đòn đánh xa (Ranged) nếu mục tiêu ở xa.
    /// </summary>
    private int ChooseBestSkillByDistance(float dist)
    {
        List<int> candidates = new List<int>();

        if (dist <= meleeRange)
        {
            AddIfAvailable(candidates, 6, dist);
            AddIfAvailable(candidates, 4, dist);
            AddIfAvailable(candidates, 3, dist);
            AddIfAvailable(candidates, 0, dist);
        }
        else if (dist <= midRange)
        {
            AddIfAvailable(candidates, 0, dist);
            AddIfAvailable(candidates, 1, dist);
            AddIfAvailable(candidates, 2, dist);
            AddIfAvailable(candidates, 4, dist);
            AddIfAvailable(candidates, 3, dist);
            AddIfAvailable(candidates, 6, dist);
        }
        else if (dist <= maxRange)
        {
            AddIfAvailable(candidates, 2, dist);
            AddIfAvailable(candidates, 5, dist);
            AddIfAvailable(candidates, 1, dist);
            AddIfAvailable(candidates, 3, dist);
            AddIfAvailable(candidates, 4, dist);
        }
        else
        {
            AddIfAvailable(candidates, 2, dist);
            AddIfAvailable(candidates, 5, dist);
            AddIfAvailable(candidates, 3, dist);
            AddIfAvailable(candidates, 4, dist);
        }

        if (candidates.Count == 0)
        {
            // Fallback: bỏ luật chống spam
            if (dist <= meleeRange)
            {
                ForceAdd(candidates, 6, dist);
                ForceAdd(candidates, 4, dist);
                ForceAdd(candidates, 0, dist);
                ForceAdd(candidates, 3, dist);
            }
            else if (dist <= midRange)
            {
                ForceAdd(candidates, 0, dist);
                ForceAdd(candidates, 1, dist);
                ForceAdd(candidates, 4, dist);
                ForceAdd(candidates, 6, dist);
                ForceAdd(candidates, 3, dist);
            }
            else if (dist <= maxRange)
            {
                ForceAdd(candidates, 2, dist);
                ForceAdd(candidates, 5, dist);
                ForceAdd(candidates, 1, dist);
                ForceAdd(candidates, 3, dist);
                ForceAdd(candidates, 4, dist);
            }
            else
            {
                ForceAdd(candidates, 2, dist);
                ForceAdd(candidates, 5, dist);
                ForceAdd(candidates, 3, dist);
                ForceAdd(candidates, 4, dist);
            }
        }

        if (candidates.Count == 0)
            return -1;

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Thêm kỹ năng vào danh sách có thể sử dụng nếu thỏa mãn các điều kiện về HP, Phase, Cooldown, Khoảng cách và không bị trùng lặp.
    /// </summary>
    /// <param name="candidates">Danh sách ứng cử viên</param>
    /// <param name="skillIndex">ID Kỹ năng cần kiểm tra</param>
    /// <param name="dist">Khoảng cách hiện tại đến Player</param>
    private void AddIfAvailable(List<int> candidates, int skillIndex, float dist)
    {
        // Skill 4 chỉ dùng khi phase 2 (HP ≤ 50%)
        if (skillIndex == 4 && !isPhase2) return;

        // Skill 3 chỉ dùng khi HP ≤ 75%
        if (skillIndex == 3)
        {
            float hpPercent = (MaxHealth > 0f) ? currentHealth / MaxHealth : 1f;
            if (hpPercent > 0.75f) return;
        }

        float reqRange = (skillIndex < skillRanges.Length)
            ? skillRanges[skillIndex]
            : 99f;

        if (!CanAttack(skillIndex)) return;

        if (dist > reqRange) return;

        // Chống spam ngay cả khi danh sách chỉ có 1
        if (recentSkills.Contains(skillIndex)) return;

        candidates.Add(skillIndex);
    }

    private bool IsMeleeSkill(int skillIndex) { return skillIndex == 4 || skillIndex == 6; }

    /// <summary>
    /// Thực hiện di chuyển chiến thuật.
    /// Boss có khả năng lùi lại (BackOff) nếu Player tiếp cận quá gần (Panic Distance), hoặc rà quanh (Strafe) để tạo khoảng cách an toàn.
    /// </summary>
    private IEnumerator TacticalMovementRoutine(bool forceBackOff)
    {
        float moveTime = repositionDuration;
        if (playerTarget == null) yield break;

        Vector2 lockedPlayerPos = playerTarget.position;
        FaceTarget(lockedPlayerPos);

        float distToLockedTarget = Vector2.Distance(transform.position, lockedPlayerPos);
        float dirXToLockedTarget = Mathf.Sign(lockedPlayerPos.x - transform.position.x);
        if (dirXToLockedTarget == 0f) dirXToLockedTarget = (facingDir == 0) ? 1f : facingDir;

        float moveDirX = 0f;

        if (forceBackOff) moveDirX = -dirXToLockedTarget;
        else if (distToLockedTarget < preferredCombatDistance - combatDistanceBuffer) moveDirX = -dirXToLockedTarget;
        else if (distToLockedTarget > preferredCombatDistance + combatDistanceBuffer) moveDirX = dirXToLockedTarget;
        else moveDirX = Random.value < 0.5f ? -dirXToLockedTarget : dirXToLockedTarget;

        float targetX = transform.position.x;
        if (moveDirX != 0f)
        {
            float step = forceBackOff ? 3.2f : strafeDistance;
            targetX = transform.position.x + moveDirX * step;
        }

        while (moveTime > 0f)
        {
            UpdateGroundLevel();
            float targetY = currentGroundY + currentTargetHeight;

            if (Mathf.Abs(transform.position.y - targetY) > 0.12f)
            {
                float newY = Mathf.MoveTowards(transform.position.y, targetY, verticalSpeed * Time.deltaTime * timeMultiplier);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            float currentTargetX = targetX;

            if (!isOverGround) currentTargetX = transform.position.x + dirXToLockedTarget * 2.5f;
            else if (moveDirX != 0f)
            {
                float currentMoveDir = Mathf.Sign(currentTargetX - transform.position.x);
                if (Mathf.Abs(currentTargetX - transform.position.x) > 0.1f && !HasGroundAhead(currentMoveDir))
                {
                    currentTargetX = transform.position.x;
                }
            }

            Vector2 fixedTargetPos = new Vector2(currentTargetX, targetY);
            transform.position = Vector2.MoveTowards(transform.position, fixedTargetPos, moveSpeed * Time.deltaTime * timeMultiplier);

            if (isOverGround && Mathf.Abs(transform.position.x - currentTargetX) < 0.12f) break;
            if (playerTarget != null && Vector2.Distance(transform.position, playerTarget.position) <= 1.4f) break;

            moveTime -= Time.deltaTime * timeMultiplier;
            yield return null;
        }
    }

    /// <summary>
    /// Phóng Raycast xuống dưới để tìm mặt đất, cập nhật cao độ hiện tại để Boss luôn bay song song với địa hình.
    /// </summary>
    private void UpdateGroundLevel()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 30f, groundLayer);
        if (hit.collider != null)
        {
            currentGroundY = hit.point.y;
            isOverGround = true;
        }
        else isOverGround = false;
    }

    /// <summary>
    /// Kiểm tra xem phía trước có mặt đất không (chống đâm đầu vào vách núi hoặc bay ra ngoài vực).
    /// </summary>
    /// <param name="dirX">Hướng di chuyển dự kiến (-1 hoặc 1)</param>
    private bool HasGroundAhead(float dirX)
    {
        if (dirX == 0f) return true;
        Vector2 checkPos = transform.position + new Vector3(dirX * 1.5f, 0f, 0f);
        float rayLength = maxFlightHeight + 10f;
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, rayLength, groundLayer);
        return hit.collider != null;
    }

    // ==========================================
    // LOGIC ÁP DỤNG SÁT THƯƠNG & TRỪ KHIÊN
    // ==========================================
    /// <summary>
    /// Ghi đè hàm sát thương cơ bản để tích hợp cơ chế Khiên Năng Lượng và bay lên trời nếu bị dồn dame quá nhanh (anti-burst).
    /// </summary>
    public override void ApplyDamage(DamageInfo info)
    {
        if (currentHealth <= 0f || isDead) return;

        // Gọi hàm từ lớp cha (EnemyBase) để thực sự trừ máu và hiện Popup
        if (info.damage > 0f)
        {
            base.ApplyDamage(info); 
        }

        if (currentHealth > 0f && !isDead)
        {
            if (info.attacker != null) playerTarget = info.attacker.transform;

            timeSinceLastDamage = 0f;
            accumulatedDamage += info.damage;

            if (accumulatedDamage >= damageToFlyUp)
            {
                currentTargetHeight = maxFlightHeight;
                accumulatedDamage = 0f;
            }
        }
    }

    /// <summary>
    /// Thực thi quá trình vận chiêu của Boss. Đóng băng di chuyển và kích hoạt Animation tương ứng.
    /// Khóa trạng thái Boss cho đến khi kỹ năng hoàn thành cast.
    /// </summary>
    /// <param name="skillIndex">ID của kỹ năng được chọn</param>
    /// <param name="castTime">Thời gian vận chiêu thực tế (ảnh hưởng bởi Time Stop)</param>
    private IEnumerator ExecuteSkillState(int skillIndex, float castTime)
    {
        currentSelectedSkill = skillIndex;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetTrigger("Attack");

        RecordAttackUsage(skillIndex);
        RegisterUsedSkill(skillIndex);

        float timer = 0f;
        while (timer < castTime)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }
    }

    /// <summary>
    /// Hàm này được gọi từ Animation Event để kích hoạt VFX/Hitbox ngay tại thời điểm vung vũ khí.
    /// Chuyển quyền xử lý chiêu thức sang BossSkillManager.
    /// </summary>
    public void TriggerVFXEvent()
    {
        if (skillManager != null && currentSelectedSkill != -1)
        {
            skillManager.SpawnVFXInstant(currentSelectedSkill, facingDir);
        }
    }

    // ==========================================
    #region BOSS BUFF & DEFENSIVE SKILLS
    // ==========================================

    /// <summary>
    /// Kích hoạt khiên năng lượng dựa trên lượng máu đã mất.
    /// Khiên này hấp thụ toàn bộ sát thương của Player trước khi trừ vào máu thật.
    /// </summary>
    public void ActivateEnergyShield()
    {
        float missingHP = MaxHealth - currentHealth;
        maxShield = missingHP * 1.5f;
        currentShield = maxShield;

        // Dùng coroutine để chịu tác động Time Stop, thay cho Invoke
        if (shieldCoroutine != null) StopCoroutine(shieldCoroutine);
        shieldCoroutine = StartCoroutine(ShieldDurationRoutine());
    }

    /// <summary>
    /// Bộ đếm thời gian hiệu lực của Khiên năng lượng.
    /// Hỗ trợ đóng băng thời gian (Time Stop) bằng cách dùng deltaTime * timeMultiplier.
    /// </summary>
    private IEnumerator ShieldDurationRoutine()
    {
        float timer = 0f;
        while (timer < 10f)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }
        currentShield = 0f;
        maxShield = 0f;
    }

    /// <summary>
    /// Kích hoạt trạng thái Nổi điên (Smack Buff). 
    /// Gia tăng tạm thời cả sức mạnh tấn công lẫn phòng thủ dựa trên cấu hình trong BossDataSO.
    /// </summary>
    public void ActivateSmackBuff()
    {
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(SmackBuffRoutine());
    }
    /// <summary>
    /// Bộ đếm thời gian duy trì Smack Buff. 
    /// Sau khi kết thúc thời gian hiệu lực (7s), tự động gỡ bỏ lượng buff đã cộng.
    /// </summary>
    private IEnumerator SmackBuffRoutine()
    {
        // Tính lượng buff dựa vào BossDataSO hoặc mặc định
        float extraATK = originalAttack * (bossData != null ? bossData.smackAttackMultiplier : 0.5f);
        float extraDEF = originalDefense * (bossData != null ? bossData.smackDefenseMultiplier : 0.7f);

        buffAttack  += extraATK;
        buffDefense += extraDEF;

        float timer = 0f;
        while (timer < 7f)
        {
            timer += Time.deltaTime * timeMultiplier; // chịu Time Stop
            yield return null;
        }

        // Gỡ buff khi hết thời gian
        buffAttack  -= extraATK;
        buffDefense -= extraDEF;
    }

    #endregion

    /// <summary>
    /// Cập nhật hướng mặt của Boss sao cho luôn nhìn về phía mục tiêu (Player).
    /// </summary>
    /// <param name="targetPos">Vị trí hiện tại của mục tiêu</param>
    private void FaceTarget(Vector2 targetPos)
    {
        float directionToTarget = Mathf.Sign(targetPos.x - transform.position.x);

        if (directionToTarget != 0f && directionToTarget != facingDir)
        {
            facingDir = (int)directionToTarget;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -facingDir;
            transform.localScale = scale;
        }
    }

    /// <summary>
    /// Xử lý logic khi Boss tử vong: Dừng toàn bộ Coroutine, vô hiệu hóa Hitbox vật lý, rơi tự do và gọi Animation chết.
    /// </summary>
    protected override void Die()
    {
        StopAllCoroutines();

        if (anim != null) anim.SetBool("isDead", true);
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 2f; }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        Destroy(gameObject);
    }

    /// <summary>
    /// Ghi nhớ kỹ năng vừa sử dụng vào bộ nhớ tạm thời (Queue).
    /// Boss sẽ không dùng lại các kỹ năng vừa xài gần nhất nhằm tạo sự đa dạng trong combat.
    /// </summary>
    /// <param name="skillIndex">ID của kỹ năng vừa tung ra</param>
    private void RegisterUsedSkill(int skillIndex)
    {
        // Loại bỏ kỹ năng khỏi hàng chờ nếu nó đã tồn tại để tránh trùng lặp

        if (recentSkills.Contains(skillIndex))
        {
            Queue<int> temp = new Queue<int>();
            foreach (int s in recentSkills)
            {
                if (s != skillIndex) temp.Enqueue(s);
            }
            recentSkills = temp;
        }

        recentSkills.Enqueue(skillIndex);

        while (recentSkills.Count > RECENT_SKILL_LIMIT)
            recentSkills.Dequeue();
    }

    /// <summary>
    /// Sàng lọc và đưa một kỹ năng vào danh sách ứng cử viên có thể sử dụng (Candidates) 
    /// nếu thỏa mãn các điều kiện về khoảng cách (Range) và Phase hiện tại.
    /// </summary>
    private void ForceAdd(List<int> candidates, int skillIndex, float dist)
    {
        // Skill 4 chỉ được mở khóa khi Boss bước sang Phase 2 (Máu < 50%)
        if (skillIndex == 4 && !isPhase2) return;

        // Skill 3 chỉ dùng khi HP ≤ 75%
        if (skillIndex == 3)
        {
            float hpPercent = (MaxHealth > 0f) ? currentHealth / MaxHealth : 1f;
            if (hpPercent > 0.75f) return;
        }

        float reqRange = (skillIndex < skillRanges.Length)
            ? skillRanges[skillIndex]
            : 99f;

        if (CanAttack(skillIndex) && dist <= reqRange)
        {
            candidates.Add(skillIndex);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, midRange);
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxRange);
    }
}
