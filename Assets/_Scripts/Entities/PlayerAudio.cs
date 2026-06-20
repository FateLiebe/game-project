using UnityEngine;

/// <summary>
/// Cầu nối (Bridge) giữa Player và AudioManager. 
/// Tự động lắng nghe các trạng thái vật lý (Chạy, Rơi, Chạm đất) để kích hoạt âm thanh lặp (Loop) phù hợp mà không làm rác file PlayerController.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    private PlayerController player;
    private Rigidbody2D rb;

    private bool wasGrounded = true;
    private bool wasRunning  = false;
    private bool fallLoopActive = false;
    private float airTime = 0f;
    private const float FALL_LOOP_DELAY = 1.5f;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        rb     = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (AudioManager.Instance == null) return;

        bool grounded = (player.CurrentState == PlayerController.PlayerState.Grounded);
        bool airborne = (player.CurrentState == PlayerController.PlayerState.Airborne);

        // --- Đáp đất ---
        if (!wasGrounded && grounded) OnLanded();

        // --- Run loop ---
        // Chạy khi: grounded + có input ngang + không đang attack
        bool isRunning = grounded
                      && Mathf.Abs(rb.linearVelocity.x) > 0.5f
                      && player.CurrentState != PlayerController.PlayerState.Attacking;

        if (isRunning && !wasRunning)
            AudioManager.Instance.StartRunLoop();
        else if (!isRunning && wasRunning)
            AudioManager.Instance.StopRunLoop();

        wasRunning = isRunning;

        // --- Fall Loop: rơi xuống > 1.5s ---
        if (airborne && rb.linearVelocity.y < -0.1f)
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

    private void OnLanded()
    {
        AudioManager.Instance.StopFallLoop();
        fallLoopActive = false;
        airTime = 0f;
        AudioManager.Instance.PlayLand();
    }

    // ============================================================
    // GỌI TỪ PLAYERCONTROLLER
    // ============================================================
    public void NotifyJump()             => AudioManager.Instance?.PlayJump();
    public void NotifyDash()             => AudioManager.Instance?.PlayDash();
    public void NotifyAttack(int combo)  => AudioManager.Instance?.PlayAttack(combo);
}
