using UnityEngine;
using UnityEngine.UI;

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
        if(canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Ẩn UI Boss lúc ban đầu
    }

    public void SetupBoss(BaseEntity boss)
    {
        currentBoss = boss;
        canvasGroup.alpha = 1f; // Hiện UI lên khi gặp Boss
    }

    public void HideBossUI()
    {
        currentBoss = null;
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (currentBoss != null)
        {
            hpSlider.value = currentBoss.currentHealth / currentBoss.MaxHealth;
            
            // Ép kiểu an toàn, kiểm tra null
            if (currentBoss is BossController bossCtrl)
            {
                if (bossCtrl.maxShield > 0)
                {
                    shieldSlider.gameObject.SetActive(true);
                    shieldSlider.value = bossCtrl.currentShield / bossCtrl.maxShield;
                }
                else
                {
                    shieldSlider.gameObject.SetActive(false);
                }
            }
            if (currentBoss.currentHealth <= 0) HideBossUI();
        }
    }
}