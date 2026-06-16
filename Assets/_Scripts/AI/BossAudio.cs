using UnityEngine;

/// <summary>
/// BossAudio — Gắn vào GameObject Boss.
/// Tự động bật tiếng vỗ cánh khi boss sống, tắt khi boss chết.
/// Âm thanh tấn công được phát qua VFX_SoundTrigger trên từng VFX prefab.
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossAudio : MonoBehaviour
{
    private BossController boss;

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

    private void OnDestroy()
    {
        // Khi boss bị Destroy (sau DeathRoutine) → tắt tiếng vỗ cánh
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBossWingFlap();
        }
    }
}
