using UnityEngine;
using System; // Bắt buộc phải có để dùng Action (Event)

public class BaseEntity : MonoBehaviour
{
    [Header("Data Configuration")]
    public CharacterDataSO baseData;

    protected float currentMoveSpeed;
    protected float currentHealth;

    // Đồng hồ thời gian cục bộ của sinh vật này
    [HideInInspector] public float timeMultiplier = 1f;

    // Sự kiện phát thanh báo cho UI biết mỗi khi máu thay đổi
    public event Action<float, float> OnHealthChanged;

    protected virtual void Start()
    {
        InitializeStats();
    }

    protected virtual void InitializeStats()
    {
        if (baseData != null)
        {
            currentMoveSpeed = baseData.moveSpeed;
            currentHealth = baseData.maxHealth;
            
            // Báo cho thanh máu cập nhật lúc mới vào game (đầy máu)
            OnHealthChanged?.Invoke(currentHealth, baseData.maxHealth);
        }
    }

    public virtual void ApplyDamage(DamageInfo info)
    {
        currentHealth -= info.damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, baseData.maxHealth); // Ép máu không bị âm
        
        // Hét lên cho thanh UI biết: "Tao vừa mất máu!"
        OnHealthChanged?.Invoke(currentHealth, baseData.maxHealth);
        
        Debug.Log($"{gameObject.name} bị trừ {info.damage} máu! Còn {currentHealth}/{baseData.maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        gameObject.SetActive(false);
        Debug.Log(gameObject.name + " đã ngỏm!");
    }
}