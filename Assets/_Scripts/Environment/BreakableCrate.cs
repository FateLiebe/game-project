using UnityEngine;
using System.Collections.Generic;

public class BreakableCrate : BaseEntity // Kế thừa BaseEntity để nhận sát thương từ Player
{
    [Header("Crate Settings")]
    public Sprite brokenSprite; // Ảnh thùng vỡ (5.png)
    public GameObject droppedItemPrefab; // Kéo Prefab cục đồ vừa tạo vào đây
    
    [Header("Loot Table (Danh sách rớt đồ)")]
    public List<ItemSO> possibleDrops; // Thêm Kiếm, Áo, Máu... vào đây

    private SpriteRenderer sr;
    private BoxCollider2D col;
    private bool isBroken = false;

    protected override void Start()
    {
        currentHealth = 10; // Máu của thùng
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
    }

    public override void ApplyDamage(DamageInfo info)
    {
        if (isBroken) return;
        
        currentHealth -= info.damage;
        
        // Nháy đỏ khi bị chém
        if (sr != null)
        {
            sr.color = Color.red;
            Invoke(nameof(ResetColor), 0.1f);
        }

        if (currentHealth <= 0) Die();
    }

    private void ResetColor() { if (sr != null) sr.color = Color.white; }

    protected override void Die()
    {
        isBroken = true;
        
        // Đổi sang ảnh thùng vỡ
        if (sr != null && brokenSprite != null) sr.sprite = brokenSprite;
        
        // Tắt va chạm cứng để Player đi xuyên qua đống đổ nát
        if (col != null) col.enabled = false;

        DropLoot();
    }

    private void DropLoot()
    {
        if (possibleDrops.Count == 0 || droppedItemPrefab == null) return;

        // Bốc thăm ngẫu nhiên 1 món đồ trong danh sách
        ItemSO droppedData = possibleDrops[Random.Range(0, possibleDrops.Count)];

        // Sinh ra cục đồ
        GameObject loot = Instantiate(droppedItemPrefab, transform.position, Quaternion.identity);
        
        // Bắn đồ nảy lên tung tóe
        Rigidbody2D rb = loot.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(new Vector2(Random.Range(-2f, 2f), 4f), ForceMode2D.Impulse);
        }

        // Gắn dữ liệu bốc thăm được vào cục đồ
        ItemPickup pickup = loot.GetComponent<ItemPickup>();
        if (pickup != null) pickup.Setup(droppedData); // ← dùng Setup() thay vì gán trực tiếp
    }
}