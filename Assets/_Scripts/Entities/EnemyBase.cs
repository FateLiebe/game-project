using UnityEngine;

public class EnemyBase : BaseEntity 
{
    private SpriteRenderer sr;
    protected Rigidbody2D rb;

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    public override void ApplyDamage(DamageInfo info)
    {
        base.ApplyDamage(info); // Trừ máu ở BaseEntity
        
        // Hiệu ứng nháy đỏ
        if (sr != null)
        {
            sr.color = Color.red;
            Invoke(nameof(ResetColor), 0.1f);
        }

        // Hiệu ứng văng lùi
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(info.knockbackForce, ForceMode2D.Impulse);
        }
    }

    private void ResetColor() 
    { 
        if (sr != null) sr.color = Color.white; 
    }

    [Header("Combat References")]
    [SerializeField] protected GameObject attackHitbox;

    public void EnableHitbox()
    {
        if (attackHitbox != null) attackHitbox.SetActive(true);
    }

    public void DisableHitbox()
    {
        if (attackHitbox != null) attackHitbox.SetActive(false);
    }
}