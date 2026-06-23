using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý thanh máu và khiên của Boss trên UI.
/// Lắng nghe sự kiện OnBossDetected / OnBossLost từ PlayerController để tự động bật tắt thanh máu và thay đổi nhạc nền.
/// </summary>
public class BossUIManager : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static BossUIManager Instance;

    public Slider hpSlider;
    public Slider shieldSlider;
    public CanvasGroup canvasGroup;

    private BaseEntity currentBoss;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        Instance = this;
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        // Subscribe vào events của Player
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.OnBossDetected += SetupBoss;
            PlayerController.Instance.OnBossLost     += HideBossUI;
        }
    }

    private void OnDestroy()
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.OnBossDetected -= SetupBoss;
            PlayerController.Instance.OnBossLost     -= HideBossUI;
        }
    }

    private void Update()
    {
        if (currentBoss == null) return;

        hpSlider.value = currentBoss.currentHealth / currentBoss.MaxHealth;

        if (currentBoss is BossController bossCtrl)
        {
            bool hasShield = bossCtrl.maxShield > 0;
            shieldSlider.gameObject.SetActive(hasShield);
            if (hasShield) shieldSlider.value = bossCtrl.currentShield / bossCtrl.maxShield;
        }

        if (currentBoss.currentHealth <= 0) HideBossUI();
    }
    #endregion

    #region PUBLIC METHODS
    public void SetupBoss(BaseEntity boss)
    {
        currentBoss = boss;
        canvasGroup.alpha = 1f;

        // Bật nhạc Boss khi thanh máu xuất hiện
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBossMusic();
    }

    public void HideBossUI()
    {
        currentBoss = null;
        canvasGroup.alpha = 0f;

        // Dừng nhạc Boss và quay lại nhạc môi trường
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
            AudioManager.Instance.RestartAmbientCycle();
        }
    }
    #endregion
}