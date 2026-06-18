using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [PHASE 3] Subscribe vào PlayerController.OnBossDetected / OnBossLost.
/// </summary>
public class BossUIManager : MonoBehaviour
{
    public static BossUIManager Instance;

    public Slider hpSlider;
    public Slider shieldSlider;
    public CanvasGroup canvasGroup;

    private BaseEntity currentBoss;

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

    public void SetupBoss(BaseEntity boss)
    {
        currentBoss = boss;
        canvasGroup.alpha = 1f;
    }

    public void HideBossUI()
    {
        currentBoss = null;
        canvasGroup.alpha = 0f;
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
}