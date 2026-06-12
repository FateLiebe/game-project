using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SupportSkillUI : MonoBehaviour
{
    public static SupportSkillUI Instance;

    public Image iconImage;
    public Image cdOverlay;
    public TextMeshProUGUI usesText;

    private void Awake() 
    { 
        Instance = this; 
        
        // Khởi đầu luôn tự động ẩn UI đi
        UpdateUI(null, 0, 0);
    }

    public void UpdateUI(ItemSO skill, float currentCD, int usesLeft)
    {
        // Ẩn các thành phần bên trong thay vì tắt luôn object cha
        if (skill == null || usesLeft <= 0)
        {
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (cdOverlay != null) cdOverlay.gameObject.SetActive(false);
            if (usesText != null) usesText.gameObject.SetActive(false);
            return;
        }

        // Hiện nội dung khi có bùa
        if (iconImage != null) iconImage.gameObject.SetActive(true);
        if (cdOverlay != null) cdOverlay.gameObject.SetActive(true);
        if (usesText != null) usesText.gameObject.SetActive(true);

        iconImage.sprite = skill.icon; 

        if (currentCD > 0) cdOverlay.fillAmount = currentCD / skill.skillCooldown;
        else cdOverlay.fillAmount = 0;

        usesText.text = usesLeft.ToString();
    }
}