using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class PlayerController : BaseEntity
{
    public enum PlayerState { Grounded, Airborne, Dashing, DashStalling, Attacking }
    public PlayerState CurrentState => currentState; // [AUDIO] Cho phép PlayerAudio đọc state

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
    private bool isPhasingThrough = false; 

    [Header("Combat System")]
    [SerializeField] private GameObject attackHitbox;
    [SerializeField] private float[] attackDurations = new float[] { 0.3f, 0.4f, 0.6f }; 
    [SerializeField] private float[] comboDamageMultipliers = new float[] { 0.8f, 1.1f, 1.3f };
    [SerializeField] private float attackInputBufferTime = 0.2f; 
    [SerializeField] [Range(0f, 1f)] private float attackMovementMultiplier = 0.3f;
    
    [SerializeField] private float perfectDodgeWindow = 0.4f; 
    [SerializeField] private float perfectDodgeCooldown = 15f;
    
    private bool perfectDodgeTriggered;

    private float perfectDodgeTimer = 0f;
    private float currentAttackDuration = 0f;
    private bool isAttacking = false;
    private Hurtbox hurtbox;

    [Header("Physics & Detection")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Boss Detection")]
    public float bossDetectionRadius = 15f; 
    public LayerMask bossLayer; 
    private BaseEntity activeBoss;

    [Header("--- SUPPORT SKILL ---")]
    public ItemSO equippedSupportSkill; 
    private float supportSkillCDTimer = 0f;
    public int currentSupportSkillUses = 0; // Số lần dùng còn lại
    private bool isSupportSkillInitialized = false;
    
    // [LOCK TARGET] Enemy được khóa mục tiêu trong 45s
    private Transform lockedTarget;
    private float lockedTargetTimer = 0f;
    private const float LOCK_DURATION = 45f;

    // [COMBAT POPUP] chống spam
    private float lastCombatWarningTime = -99f;
    private const float COMBAT_WARNING_COOLDOWN = 2f;
    
    private Animator anim;
    private Rigidbody2D rb;
    private PlayerAudio playerAudio; // [AUDIO]
    private Coroutine dashCoroutine;
    private Coroutine attackCoroutine;
    private Vector2 currentDashDirection; 
    private float lastAttackTime;
    private float lastAttackInputTime = -10f;
    private float lastSKeyPressTime;
    private float lastJumpTime = -10f;
    private float horizontalInput;
    private float verticalInput;
    private float originalGravity;

    private float lastGroundedTime = 0f;
    private const float COYOTE_TIME = 0.12f;

    // Thêm vào phần khai báo biến
    private int groundedFrameCount = 0;
    private int notGroundedFrameCount = 0;
    private const int GROUND_CONFIRM_FRAMES = 3;
    
    private Collider2D[] threatColliders = new Collider2D[20];
    
    // [PHASE 2] Buffer tái sử dụng cho OverlapCircleNonAlloc — tránh cấp phát RAM mỗi frame
    private static readonly Collider2D[] _enemyScanBuffer = new Collider2D[24];
    
    // Lưu BaseEntity để đồng nhất danh tính của kẻ địch
    private List<BaseEntity> ignoredAttackers = new List<BaseEntity>();

    // ==========================================
    // [PHASE 3] EVENTS — UI subscribe, Player chỉ phát signal
    // ==========================================
    public static PlayerController Instance { get; private set; }

    /// <summary>Dodge cooldown thay đổi — (currentTimer, maxCooldown)</summary>
    public event Action<float, float> OnDodgeCooldownChanged;

    /// <summary>Support skill state thay đổi — (skill, cdTimer, usesLeft)</summary>
    public event Action<ItemSO, float, int> OnSupportSkillUpdated;

    /// <summary>Boss mới được phát hiện trong tầm</summary>
    public event Action<BaseEntity> OnBossDetected;

    /// <summary>Boss đã rời khỏi tầm hoặc chết</summary>
    public event Action OnBossLost;

    // ==========================================
    #region CORE UNITY METHODS
    // ==========================================

    private void Awake()
    {
        // Đăng ký singleton — dùng bởi UI scripts để subscribe events
        Instance = this;
    }

    protected override void Start()
    {
        base.Start(); 
        rb = GetComponent<Rigidbody2D>();
        hurtbox = GetComponentInChildren<Hurtbox>(); 
        anim = GetComponent<Animator>();
        playerAudio = GetComponent<PlayerAudio>(); // [AUDIO]
        originalGravity = rb.gravityScale;
        
        if (baseData != null) currentDashCharges = baseData.maxDashes;
    }

    private void Update()
    {
        // [FIX CHẶN INPUT]: Dừng mọi tương tác nếu game không ở trạng thái Gameplay
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
        {
            // Triệt tiêu vận tốc ngang để không bị trượt đi
            horizontalInput = 0;
            verticalInput = 0;
            if (currentState != PlayerState.Dashing && currentState != PlayerState.DashStalling)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            UpdateAnimations();
            return; // Dừng chạy các code bên dưới
        }

        HandleDashRecharge();
        if (perfectDodgeTimer > 0)
        {
            perfectDodgeTimer -= Time.deltaTime;
        }
        // [PHASE 3] Phát event thay vì gọi UIManager trực tiếp
        OnDodgeCooldownChanged?.Invoke(perfectDodgeTimer, perfectDodgeCooldown);

        // Chặn input khi inventory mở — nhưng vẫn cho B được xử lý bởi StatsUIManager
        if (StatsUIManager.Instance != null && StatsUIManager.Instance.IsOpen)
        {
            horizontalInput = 0; 
            verticalInput = 0;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); 
            UpdateAnimations(); 
            // [LOCK TARGET] tiếp tục đếm thời gian
            UpdateLockedTarget();
            return; 
        }

        HandleBossDetection();

        HandleSupportSkill();

        HandleInput();
        HandleComboReset();
        CheckGrounded();
        Flip();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (currentState == PlayerState.Dashing || currentState == PlayerState.DashStalling) return;

        float targetSpeed = horizontalInput * currentMoveSpeed;
        if (isAttacking) targetSpeed *= attackMovementMultiplier;

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }
    
    #endregion

    // ==========================================
}
