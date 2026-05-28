using UnityEngine;
using System.Collections;

public class EnemyController : EnemyBase
{
    // BỘ NÃO FSM: 6 Trạng thái cốt lõi
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }
    
    [Header("AI State")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Movement & Vision Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float lineOfSight = 6f;       // Tầm nhìn quét Player
    [SerializeField] private float attackRange = 1.2f;     // Tầm vung kiếm
    [SerializeField] private float idleDuration = 1.5f;    // Thời gian đứng thở
    
    [Header("Detection (Kẻ tia)")]
    [SerializeField] private Transform wallCheck;          // Điểm gắn ở bụng để check tường
    [SerializeField] private Transform ledgeCheck;         // Điểm gắn ở mép chân check vực
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    private Animator anim;
    private Transform playerTarget;
    
    private float stateTimer;
    private int facingDir = 1; // -1: quay trái, 1: quay phải
    private bool canAction = true; // Khóa AI khi đang bị đánh hoặc đang chém

    protected override void Start()
    {
        base.Start(); // Gọi Start của EnemyBase để lấy máu, nó tự gán 'rb' luôn rồi!
        anim = GetComponent<Animator>();

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
        
        // Khởi tạo FSM
        SwitchState(EnemyState.Patrol);
    }

    private void Update()
    {
        // Nếu đã chết hoặc đang bị choáng/đang chém thì không cho AI suy nghĩ
        if (currentState == EnemyState.Dead || !canAction) return;

        // VÒNG LẶP SUY NGHĨ (FSM)
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
                // Logic Attack được xử lý qua Coroutine
                break;
        }

        UpdateAnimations();
    }

    // ==========================================
    // CÁC HÀM XỬ LÝ TRẠNG THÁI
    // ==========================================
    private void UpdateIdle()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        
        // Luôn kiểm tra xem có ai đứng sát mặt không
        if (DetectPlayer()) 
        {
            float dist = Vector2.Distance(transform.position, playerTarget.position);
            if (dist <= attackRange) SwitchState(EnemyState.Attack); // Tấn công ngay nếu sát mặt
            else SwitchState(EnemyState.Chase); // Rượt nếu ở xa
            return;
        }

        // Đếm ngược chậm đi nếu bị ngưng đọng thời gian
        stateTimer -= Time.deltaTime * timeMultiplier; 
        if (stateTimer <= 0)
        {
            Flip();
            SwitchState(EnemyState.Patrol);
        }
    }

    private void UpdatePatrol()
    {
        // Vừa đi vừa check vực và tường
        if (CheckWall() || !CheckLedge())
        {
            SwitchState(EnemyState.Idle);
            return;
        }

        // Tốc độ đi tuần bị bóp theo timeMultiplier
        rb.linearVelocity = new Vector2(patrolSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);

        // Đang đi tuần mà quét thấy Player
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

        // Nếu Player chạy ra khỏi tầm nhìn (x2 để đuổi dai hơn 1 chút)
        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distToPlayer > lineOfSight * 1.5f)
        {
            playerTarget = null;
            SwitchState(EnemyState.Idle);
            return;
        }

        // Nếu Player vào tầm chém
        if (distToPlayer <= attackRange)
        {
            SwitchState(EnemyState.Attack);
            return;
        }

        // Quay mặt theo Player và rượt
        FacePlayer();
        
        // Cảnh báo: Rượt nhưng vẫn phải check vực kẻo rơi xuống hố
        if (!CheckLedge() || CheckWall())
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        else
        {
            // Tốc độ rượt đuổi bị bóp theo timeMultiplier
            rb.linearVelocity = new Vector2(chaseSpeed * facingDir * timeMultiplier, rb.linearVelocity.y);
        }
    }

    private IEnumerator PerformAttack()
    {
        canAction = false;
        rb.linearVelocity = Vector2.zero; // Dừng lại để chém
        
        anim.SetTrigger("Attack");

        // Dùng vòng lặp bị ảnh hưởng bởi làm chậm thay vì WaitForSeconds(1f)
        float timer = 0f;
        while(timer < 1f)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }
        
        canAction = true;
        SwitchState(EnemyState.Chase); // Chém xong check xem còn đuổi được không
    }

    public void CancelCurrentAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.SetActive(false);
        }
    }

    // ==========================================
    // HỆ THỐNG GIAO TIẾP VỚI ENEMY_BASE
    // ==========================================
    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info); // Vẫn gọi nháy đỏ và văng lùi ở file Base

        if (currentHealth > 0)
        {
            // Ngắt các hành động hiện tại
            StopAllCoroutines();
            SwitchState(EnemyState.Hurt);
            anim.SetTrigger("Hurt");
            
            // Tìm ra kẻ đánh mình để quay mặt lại thù hận
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

        // Thời gian choáng lâu hơn nếu bị Time Stop
        float timer = 0f;
        while(timer < 0.4f)
        {
            timer += Time.deltaTime * timeMultiplier;
            yield return null;
        }

        canAction = true;
        SwitchState(EnemyState.Chase); // Ăn đòn xong là điên lên rượt luôn
    }

    protected override void Die()
    {
        base.Die();
        StopAllCoroutines();
        SwitchState(EnemyState.Dead);
        anim.SetBool("isDead", true);
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;
        this.enabled = false; // Tắt bộ não AI
    }

    // ==========================================
    // HỆ THỐNG GIÁC QUAN (SENSORS)
    // ==========================================
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
        if (ledgeCheck == null) return true;
        return Physics2D.Raycast(ledgeCheck.position, Vector2.down, 0.5f, groundLayer);
    }

    private bool DetectPlayer()
    {
        // 1. QUÉT TẦM XA (Tia đỏ): Để bắt đầu đuổi theo khi thấy mục tiêu từ xa
        if (wallCheck != null)
        {
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, lineOfSight, playerLayer);
            if (hit.collider != null)
            {
                playerTarget = hit.transform;
                return true;
            }
        }

        // 2. QUÉT CỰ LY GẦN (Vòng tròn cam): Đứng sát mặt là dính ngay, không cần nhìn
        Collider2D closeHit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (closeHit != null)
        {
            playerTarget = closeHit.transform;
            return true;
        }

        return false;
    }

    // ==========================================
    // TIỆN ÍCH DI CHUYỂN & ANIMATION
    // ==========================================
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
        // Gửi vận tốc cho Animator để biết đang đi chậm (Patrol) hay chạy nhanh (Chase)
        anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
    }

    // Khối code này chỉ chạy trong màn hình Scene để dễ Debug, không hiện ra trong game thật
    private void OnDrawGizmos()
    {
        // 1. Vẽ tia quét Player (Màu Đỏ)
        if (wallCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * facingDir * lineOfSight);
        }

        // 2. Vẽ vòng tròn tầm chém Attack Range (Màu Cam)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}