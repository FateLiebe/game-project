using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng âm thanh vòng lặp (Loop) đặc trưng của Boss.
/// Hiện tại dùng để phát âm thanh vỗ cánh liên tục từ lúc Boss xuất hiện cho đến lúc bị tiêu diệt. 
/// Các kỹ năng đòn đánh khác được xử lý riêng bên VFX_SoundTrigger.
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossAudio : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    private BossController boss;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        boss = GetComponent<BossController>();
    }

    private void Start()
    {
        // Bắt đầu tiếng vỗ cánh ngay khi boss xuất hiện
        if (AudioManager.Instance != null && AudioManager.Instance.bossWingFlapClip != null)
        {
            AudioManager.Instance.StartBossWingFlap(AudioManager.Instance.bossWingFlapClip);
        }
    }

    private void Update()
    {
        // Nếu boss chết, dừng tiếng vỗ cánh
        if (boss.isDead)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBossWingFlap();
            }
        }
    }

    private void OnDestroy()
    {
        // Khi boss bị Destroy (sau DeathRoutine) → tắt tiếng vỗ cánh
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBossWingFlap();
        }
    }
    #endregion
}
