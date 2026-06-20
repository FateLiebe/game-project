using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Giao diện Thanh Máu, EXP và Level của Thực thể (Player, Quái).
/// Tích hợp cơ chế bù trừ Scale: Ngăn chặn thanh máu bị lật ngược chữ khi Entity quay đầu.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Tooltip("Kéo đối tượng chứa BaseEntity (Player hoặc Slime) vào đây")]
    [SerializeField] private BaseEntity entity;
    
    [Header("UI Components")]
    [Tooltip("Kéo UI Slider thanh máu vào đây")]
    [SerializeField] private Slider healthSlider;
    
    [Tooltip("Kéo TextMeshPro hiển thị Level vào đây (Nếu có)")]
    [SerializeField] private TextMeshProUGUI levelText;
    
    [Tooltip("Kéo UI Slider thanh EXP vào đây (Chỉ dùng cho Player)")]
    [SerializeField] private Slider expSlider;

    private void OnEnable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged += UpdateHealthBar;
            entity.OnLevelChanged += UpdateLevelText;
            entity.OnExpChanged += UpdateExpBar;
        }
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged -= UpdateHealthBar;
            entity.OnLevelChanged -= UpdateLevelText;
            entity.OnExpChanged -= UpdateExpBar;
        }
    }

    // --- HÀM FIX: Triệt tiêu hoàn toàn sự lật của TOÀN BỘ thanh máu ---
    private void LateUpdate()
    {
        if (transform.parent != null)
        {
            Vector3 currentScale = transform.localScale;
            
            // Ép Scale X của TOÀN BỘ cụm UI ngược dấu với Scale X của quái.
            // Quái quay trái (-1) x UI tự lật (-1) = Cả thanh máu đứng im hướng sang phải (+1)
            currentScale.x = Mathf.Abs(currentScale.x) * Mathf.Sign(transform.parent.localScale.x);
            
            transform.localScale = currentScale;
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = level.ToString(); // Đổi số nguyên thành chuỗi ký tự
        }
    }

    private void UpdateExpBar(float currentExp, float maxExp)
    {
        if (expSlider != null)
        {
            expSlider.maxValue = maxExp;
            expSlider.value = currentExp;
        }
    }
}