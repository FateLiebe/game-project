using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    public float preferredCombatDistance = 6.2f;
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
    // Boss HP = EnemyBase.MaxHealth × 8
    public override float MaxHealth => (150f + ((currentLevel - 1) * 30f)) * 8f;

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

    protected override void Start()
    {
        // 1. Tìm Player và set level Boss = Player + 3
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTarget = p.transform;
            BaseEntity playerEntity = p.GetComponent<BaseEntity>();
            if (playerEntity != null)
                this.currentLevel = playerEntity.currentLevel + 3;
        }

        // 2. Gọi base.Start() để EnemyBase khởi tạo rb, sr, và cooldown arrays
        base.Start();

        // 3. Tính chỉ số boss dựa trên level (công thức EnemyBase)
        float enemyBaseATK = 12f + ((currentLevel - 1) * 4f);
        float enemyBaseDEF = 3f  + ((currentLevel - 1) * 1f);

        // buffAttack/buffDefense cộng thêm vào property Attack/Defense của BaseEntity
        // Mục tiêu: Attack = EnemyBaseATK × 1.8 → buff thêm 0.8×
        //           Defense = EnemyBaseDEF × 2.0 → buff thêm 1.0×
        buffAttack  = enemyBaseATK * 0.8f;
        buffDefense = enemyBaseDEF * 1.0f;

        // HP đã được override qua MaxHealth property, chỉ cần bơm đầy
        currentHealth = MaxHealth;

        // Lưu lại chỉ số gốc để SmackBuff tham chiếu
        originalAttack  = Attack;
        originalDefense = Defense;

        // 4. Setup components
        anim = GetComponent<Animator>();
        skillManager = GetComponent<BossSkillManager>();

        if (rb != null) rb.gravityScale = 0f;
        currentTargetHeight = minFlightHeight;
        StartCoroutine(BehaviorTreeLoop());
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

    private int ChooseBestSkillByDistance(float dist)
    {
        List<int> candidates = new List<int>();

        if (dist <= meleeRange)
        {
            AddIfAvailable(candidates, 6, dist);
            AddIfAvailable(candidates, 4, dist);
            AddIfAvailable(candidates, 0, dist);
        }
        else if (dist <= midRange)
        {
            AddIfAvailable(candidates, 0, dist);
            AddIfAvailable(candidates, 1, dist);
            AddIfAvailable(candidates, 4, dist);
            AddIfAvailable(candidates, 6, dist);
        }
        else if (dist <= maxRange)
        {
            AddIfAvailable(candidates, 2, dist);
            AddIfAvailable(candidates, 5, dist);
            AddIfAvailable(candidates, 1, dist);
        }
        else
        {
            AddIfAvailable(candidates, 2, dist);
            AddIfAvailable(candidates, 5, dist);
        }

        if (candidates.Count == 0) return -1;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void AddIfAvailable(List<int> candidates, int skillIndex, float dist)
    {
        // YÊU CẦU ĐẶC BIỆT: Skill 4 (Smack Buff) chỉ dùng ở Phase 2
        if (skillIndex == 4 && !isPhase2) return;

        float reqRange = (skillIndex < skillRanges.Length) ? skillRanges[skillIndex] : 99f;
        if (CanAttack(skillIndex) && dist <= reqRange)
        {
            candidates.Add(skillIndex);
        }
    }

    private bool IsMeleeSkill(int skillIndex) { return skillIndex == 4 || skillIndex == 6; }

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
    public override void ApplyDamage(DamageInfo info)
    {
        if (currentHealth <= 0f || isDead) return;

        // Xử lý Khiên chắn đòn trước (Skill 3)
        // if (currentShield > 0f)
        // {
        //     currentShield -= info.damage;
        //     if (currentShield < 0f) 
        //     {
        //         info.damage = Mathf.Abs(currentShield); // Lượng dame dư xuyên qua khiên
        //         currentShield = 0f;
        //     }
        //     else
        //     {
        //         info.damage = 0f; // Khiên đỡ hết
        //     }
        // }

        // Lượng dame còn lại truyền cho BaseEntity trừ máu
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

    private IEnumerator ExecuteSkillState(int skillIndex, float castTime)
    {
        currentSelectedSkill = skillIndex;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetTrigger("Attack");

        RecordAttackUsage(skillIndex);

        float timer = 0f;
        while (timer < castTime)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }
    }

    public void TriggerVFXEvent()
    {
        if (skillManager != null && currentSelectedSkill != -1)
        {
            skillManager.SpawnVFXInstant(currentSelectedSkill, facingDir);
        }
    }

    // ==========================================
    // CÁC HÀM BUFF CHO BOSS (Được gọi từ BossSkillManager)
    // ==========================================
    public void ActivateEnergyShield()
    {
        float missingHP = MaxHealth - currentHealth;
        maxShield = missingHP * 1.5f;
        currentShield = maxShield;

        // Dùng coroutine để chịu tác động Time Stop, thay cho Invoke
        if (shieldCoroutine != null) StopCoroutine(shieldCoroutine);
        shieldCoroutine = StartCoroutine(ShieldDurationRoutine());
    }
    //private void DeactivateShield() { currentShield = 0f; maxShield = 0f; }

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

    public void ActivateSmackBuff()
    {
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(SmackBuffRoutine());
    }
    private IEnumerator SmackBuffRoutine()
    {
        // Mục tiêu: ATK tạm thời = originalAttack × 1.5, DEF tạm thời = originalDefense × 1.7
        // originalAttack/Defense đã là chỉ số boss (EnemyBase × 1.8/2.0)
        // Chỉ cần cộng thêm delta vào buffAttack/buffDefense
        float extraATK = originalAttack * 0.5f;   // 50% thêm vào
        float extraDEF = originalDefense * 0.7f;  // 70% thêm vào

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