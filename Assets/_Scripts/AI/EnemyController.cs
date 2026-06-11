using UnityEngine;
using System.Collections;

public class EnemyController : EnemyBase
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }
    
    [Header("AI State")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Movement & Vision Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float lineOfSight = 6f;       
    [SerializeField] private float attackRange = 1.8f;     
    [SerializeField] private float idleDuration = 1.5f;    
    
    // [MỚI THÊM]: Cờ xác định quái vật có bay hay không
    [SerializeField] public bool isFlying = false;

    [Header("Detection (Kẻ tia)")]
    [SerializeField] private Transform wallCheck;          
    [SerializeField] private Transform ledgeCheck;         
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    private Animator anim;
    private Transform playerTarget;
    
    private float stateTimer;
    private int facingDir = 1; 
    private bool canAction = true; 

    protected override void Start()
    {
        base.Start(); 
        anim = GetComponent<Animator>();

        // [ĐÃ SỬA]: Chỉ ép vị trí xuống đất nếu KHÔNG PHẢI quái bay
        if (!isFlying)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                Vector2.down,
                10f,
                LayerMask.GetMask("Ground")
            );

            if (hit.collider != null)
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y;
                transform.position = pos;
            }
        }
        else
        {
            // Nếu là quái bay, tắt trọng lực để nó lơ lửng
            if (rb != null) rb.gravityScale = 0f;
        }
        
        SwitchState(EnemyState.Patrol);
    }

    protected override void Update()
    {
        base.Update(); 

        if (currentState == EnemyState.Dead || !canAction) return;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Attack:
                break;
        }

        UpdateAnimations();
    }

    private void UpdateIdle()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        
        if (DetectPlayer()) 
        {
            float dist = Vector2.Distance(transform.position, playerTarget.position);
            
            if (dist <= attackRange && CanAttack(0)) 
            {
                SwitchState(EnemyState.Attack);
            }
            else 
            {
                SwitchState(EnemyState.Chase); 
            }
            return;
        }

        stateTimer -= Time.deltaTime * timeMultiplier; 
        if (stateTimer <= 0)
        {
            Flip();
            SwitchState(EnemyState.Patrol);
        }
    }

    private void UpdatePatrol()
    {
        if (CheckWall() || !CheckLedge())
        {
            SwitchState(EnemyState.Idle);
            return;
        }

        rb.linearVelocity = new Vector2(patrolSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);

        if (DetectPlayer())
        {
            SwitchState(EnemyState.Chase);
        }
    }

    private void UpdateChase()
    {
        if (playerTarget == null)
        {
            SwitchState(EnemyState.Idle);
            return;
        }

        BaseEntity targetEntity = playerTarget.GetComponentInParent<BaseEntity>();
        if (targetEntity == null || targetEntity.currentHealth <= 0)
        {
            playerTarget = null;
            SwitchState(EnemyState.Idle); 
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distToPlayer > lineOfSight * 1.5f)
        {
            playerTarget = null;
            SwitchState(EnemyState.Idle);
            return;
        }

        if (distToPlayer <= attackRange)
        {
            // Tạm thời vẫn dùng CanAttack(0) để test logic rượt đuổi, sẽ sửa lại khi làm hệ thống đa kỹ năng cho Boss
            if (CanAttack(0))
            {
                SwitchState(EnemyState.Attack);
            }
            else
            {
                FacePlayer();
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            return;
        }

        FacePlayer();
        
        if (!CheckLedge() || CheckWall())
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(chaseSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);
        }
    }

    private IEnumerator PerformAttack()
    {
        canAction = false;
        rb.linearVelocity = Vector2.zero; 
        
        anim.SetTrigger("Attack");

        RecordAttackUsage(0); 

        float timer = 0f;
        while(timer < 1f)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }
        
        canAction = true;
        SwitchState(EnemyState.Chase); 
    }

    public void CancelCurrentAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.SetActive(false);
        }
    }

    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info); 

        if (currentHealth > 0)
        {
            StopAllCoroutines();
            SwitchState(EnemyState.Hurt);
            anim.SetTrigger("Hurt");
            
            if (info.attacker != null)
            {
                playerTarget = info.attacker.transform;
                FacePlayer();
            }

            StartCoroutine(RecoverFromHurt());
        }
    }

    private IEnumerator RecoverFromHurt()
    {
        canAction = false;

        float timer = 0f;
        while(timer < 0.4f)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }

        canAction = true;
        SwitchState(EnemyState.Chase); 
    }

    protected override void Die()
    {
        CancelCurrentAttackHitbox();
        StopAllCoroutines(); 
        
        SwitchState(EnemyState.Dead);
        anim.SetBool("isDead", true);
        rb.linearVelocity = Vector2.zero;
        
        GetComponent<Collider2D>().enabled = false;
        
        // Nếu là quái bay thì cho nó rơi từ từ xuống đất khi chết cho chân thực
        if (isFlying && rb != null) rb.gravityScale = 2f; 

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.833f);
        Destroy(gameObject); 
    }

    private void SwitchState(EnemyState newState)
    {
        currentState = newState;
        if (newState == EnemyState.Idle) stateTimer = idleDuration;
        else if (newState == EnemyState.Attack) StartCoroutine(PerformAttack());
    }

    private bool CheckWall()
    {
        if (wallCheck == null) return false;
        return Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, 0.2f, groundLayer);
    }

    private bool CheckLedge()
    {
        // [ĐÃ SỬA]: Nếu là quái bay thì không bao giờ sợ vực sâu
        if (isFlying) return true; 

        if (ledgeCheck == null) return true;
        return Physics2D.Raycast(ledgeCheck.position, Vector2.down, 0.5f, groundLayer);
    }

    private bool DetectPlayer()
    {
        if (wallCheck != null)
        {
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, lineOfSight, playerLayer);
            if (hit.collider != null)
            {
                BaseEntity target = hit.collider.GetComponentInParent<BaseEntity>();
                if (target != null && target.currentHealth > 0)
                {
                    playerTarget = target.transform;
                    return true;
                }
            }
        }

        Collider2D closeHit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (closeHit != null)
        {
            BaseEntity target = closeHit.GetComponentInParent<BaseEntity>();
            if (target != null && target.currentHealth > 0)
            {
                playerTarget = target.transform;
                return true;
            }
        }

        return false;
    }

    private void Flip()
    {
        facingDir *= -1;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void FacePlayer()
    {
        if (playerTarget == null) return;
        if (playerTarget.position.x > transform.position.x && facingDir == -1) Flip();
        else if (playerTarget.position.x < transform.position.x && facingDir == 1) Flip();
    }

    private void UpdateAnimations()
    {
        // Nếu là quái bay, có thể dùng 1 anim bay lơ lửng suốt nên không cần set speed
        if (!isFlying) anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
    }

    private void OnDrawGizmos()
    {
        if (wallCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * facingDir * lineOfSight);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}