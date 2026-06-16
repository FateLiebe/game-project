using UnityEngine;
using System.Collections;

/// <summary>
/// PlayerAudio — Gắn vào GameObject Player.
/// Tự động lắng nghe trạng thái Player và gọi AudioManager phát âm thanh phù hợp.
/// Không cần kéo thả AudioClip ở đây — tất cả clip đều đặt tại AudioManager.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    // ============================================================
    // THAM CHIẾU NỘI BỘ
    // ============================================================
    private PlayerController player;
    private Rigidbody2D rb;

    // ============================================================
    // TRẠNG THÁI NỘI BỘ
    // ============================================================
    private bool wasGrounded = true;
    private bool isFalling   = false;
    private float airTime    = 0f;           // Thời gian lơ lửng liên tiếp
    private const float FALL_LOOP_DELAY = 1.5f; // 1.5s → bật fall loop

    private bool fallLoopActive = false;
    private int  lastComboStep  = 0;         // Theo dõi comboStep để detect attack mới

    // ============================================================
    // KHỞI TẠO
    // ============================================================
    private void Awake()
    {
        player = GetComponent<PlayerController>();
        rb     = GetComponent<Rigidbody2D>();
    }

    // ============================================================
    // UPDATE — theo dõi trạng thái mỗi frame
    // ============================================================
    private void Update()
    {
        if (AudioManager.Instance == null) return;

        bool grounded = (player.CurrentState == PlayerController.PlayerState.Grounded);

        // --- Đáp đất ---
        if (!wasGrounded && grounded)
        {
            OnLanded();
        }

        // --- Fall Loop: lơ lửng > 1.5s ---
        bool airborne = (player.CurrentState == PlayerController.PlayerState.Airborne);
        if (airborne && rb.linearVelocity.y < -0.1f) // Đang rơi xuống
        {
            airTime += Time.deltaTime;
            if (airTime >= FALL_LOOP_DELAY && !fallLoopActive)
            {
                fallLoopActive = true;
                AudioManager.Instance.StartFallLoop();
            }
        }
        else
        {
            airTime = 0f;
            if (fallLoopActive)
            {
                fallLoopActive = false;
                AudioManager.Instance.StopFallLoop();
            }
        }

        wasGrounded = grounded;
    }

    // ============================================================
    // SỰ KIỆN ĐÁP ĐẤT
    // ============================================================
    private void OnLanded()
    {
        AudioManager.Instance.StopFallLoop();
        fallLoopActive = false;
        airTime = 0f;
        AudioManager.Instance.PlayLand();
    }

    // ============================================================
    // CÁC HÀM ĐƯỢC GỌI TỪ ANIMATION EVENT
    // Gắn Event vào các Animation Clip tương ứng trong Unity
    // ============================================================

    /// <summary>Animation Event: Gắn vào frame đánh của Attack1/2/3</summary>
    public void OnAttackSound(int comboStep)
    {
        AudioManager.Instance.PlayAttack(comboStep);
    }

    /// <summary>Animation Event: Gắn vào frame bắt đầu Dash</summary>
    public void OnDashSound()
    {
        AudioManager.Instance.PlayDash();
    }

    /// <summary>Animation Event: Gắn vào frame bắt đầu Jump</summary>
    public void OnJumpSound()
    {
        AudioManager.Instance.PlayJump();
    }

    /// <summary>Animation Event: Gắn vào cuối animation Run → Idle</summary>
    public void OnRunToIdleSound()
    {
        AudioManager.Instance.PlayRunToIdle();
    }

    // ============================================================
    // GỌI TRỰC TIẾP TỪ PLAYERCONTROLLER (thêm 1 dòng vào các hàm)
    // Xem hướng dẫn ở cuối file
    // ============================================================

    /// <summary>Gọi từ PlayerController.Jump()</summary>
    public void NotifyJump()
    {
        AudioManager.Instance.PlayJump();
    }

    /// <summary>Gọi từ PlayerController.PerformDash() đầu coroutine</summary>
    public void NotifyDash()
    {
        AudioManager.Instance.PlayDash();
    }

    /// <summary>Gọi từ PlayerController.PerformAttack() khi comboStep tăng</summary>
    public void NotifyAttack(int comboStep)
    {
        AudioManager.Instance.PlayAttack(comboStep);
    }
}

/*
 * ================================================================
 * HƯỚNG DẪN TÍCH HỢP VÀO PLAYERCONTROLLER.CS
 * ================================================================
 * Thêm 1 biến vào PlayerController:
 *   private PlayerAudio playerAudio;
 *
 * Trong Start():
 *   playerAudio = GetComponent<PlayerAudio>();
 *
 * Trong Jump():
 *   playerAudio?.NotifyJump();
 *
 * Trong PerformDash() — ngay sau dòng "currentState = PlayerState.Dashing;":
 *   playerAudio?.NotifyDash();
 *
 * Trong PerformAttack() — ngay sau "comboStep++":
 *   playerAudio?.NotifyAttack(comboStep);
 * ================================================================
 */
