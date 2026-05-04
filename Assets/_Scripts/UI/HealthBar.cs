using UnityEngine;
using UnityEngine.UI; // Để tương tác với UI Slider

public class HealthBar : MonoBehaviour
{
    [Tooltip("Kéo đối tượng chứa BaseEntity (Player hoặc Slime) vào đây")]
    [SerializeField] private BaseEntity entity;
    
    [Tooltip("Kéo UI Slider vào đây")]
    [SerializeField] private Slider healthSlider;

    // Lắng nghe sự kiện khi thanh máu được bật lên
    private void OnEnable()
    {
        if (entity != null)
            entity.OnHealthChanged += UpdateHealthBar;
    }

    // Bỏ lắng nghe khi thanh máu bị tắt (để chống lỗi rò rỉ bộ nhớ)
    private void OnDisable()
    {
        if (entity != null)
            entity.OnHealthChanged -= UpdateHealthBar;
    }

    // Hàm này sẽ tự động chạy mỗi khi BaseEntity bị trừ máu
    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
}