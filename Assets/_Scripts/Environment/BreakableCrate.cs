using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class RarityWeight
{
    public ItemRarity rarity;
    [Tooltip("Trọng số. Số càng cao càng dễ ra.")]
    public float weight = 10f;
}

[System.Serializable]
public class TypeWeight
{
    public ItemType itemType;
    public float weight = 10f;
}

public class BreakableCrate : BaseEntity
{
    [Header("Crate Settings")]
    public Sprite brokenSprite;
    public GameObject droppedItemPrefab;

    [Header("Loot Pool — Kéo tất cả ItemSO vào đây")]
    public List<ItemSO> itemPool = new List<ItemSO>();

    [Header("Type Weights — Trọng số loại đồ")]
    public List<TypeWeight> typeWeights = new List<TypeWeight>()
    {
        new TypeWeight { itemType = ItemType.Consumable,   weight = 25f },
        new TypeWeight { itemType = ItemType.Helmet,       weight = 10f },
        new TypeWeight { itemType = ItemType.Weapon,       weight = 10f },
        new TypeWeight { itemType = ItemType.Armor,        weight = 10f },
        new TypeWeight { itemType = ItemType.Pants,        weight = 10f },
        new TypeWeight { itemType = ItemType.Boots,        weight = 10f },
        new TypeWeight { itemType = ItemType.Accessory,    weight = 10f },
        new TypeWeight { itemType = ItemType.SupportSkill, weight = 15f },
    };

    [Header("Rarity Weights — Trọng số chất lượng")]
    public List<RarityWeight> rarityWeights = new List<RarityWeight>()
    {
        new RarityWeight { rarity = ItemRarity.Common,    weight = 50f },
        new RarityWeight { rarity = ItemRarity.Rare,      weight = 35f },
        new RarityWeight { rarity = ItemRarity.Epic,      weight = 25f },
        new RarityWeight { rarity = ItemRarity.Legendary, weight = 15f },
    };

    [Header("Drop Settings")]
    public int dropCount = 1;

    private SpriteRenderer sr;
    private BoxCollider2D col;
    private bool isBroken = false;

    protected override void Start()
    {
        currentHealth = 10;
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
    }

    public override void ApplyDamage(DamageInfo info)
    {
        if (isBroken) return;
        currentHealth -= info.damage;
        if (sr != null) { sr.color = Color.red; Invoke(nameof(ResetColor), 0.1f); }
        if (currentHealth <= 0) Die();
    }

    private void ResetColor() { if (sr != null) sr.color = Color.white; }

    protected override void Die()
    {
        isBroken = true;
        if (sr != null && brokenSprite != null) sr.sprite = brokenSprite;
        if (col != null) col.enabled = false;
        DropLoot();
    }

    private void DropLoot()
    {
        if (droppedItemPrefab == null || itemPool.Count == 0) return;

        for (int i = 0; i < dropCount; i++)
        {
            // Bước 1: Roll loại đồ
            ItemType rolledType = RollWeighted(typeWeights, t => t.weight).itemType;

            // Bước 2: Roll chất lượng
            ItemRarity rolledRarity = RollWeighted(rarityWeights, r => r.weight).rarity;

            // Bước 3: Lọc pool theo type + rarity
            List<ItemSO> candidates = itemPool
                .Where(item => item != null 
                            && item.itemType == rolledType 
                            && item.rarity == rolledRarity)
                .ToList();

            // Nếu không có đồ khớp cả type lẫn rarity, nới lỏng: chỉ lọc theo type
            if (candidates.Count == 0)
                candidates = itemPool.Where(item => item != null && item.itemType == rolledType).ToList();

            // Vẫn trống thì bỏ qua lượt drop này
            if (candidates.Count == 0) continue;

            // Bước 4: Random đều trong danh sách đã lọc
            ItemSO selected = candidates[Random.Range(0, candidates.Count)];

            // Bước 5: Spawn
            Vector3 spawnPos = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            GameObject loot = Instantiate(droppedItemPrefab, spawnPos, Quaternion.identity);

            Rigidbody2D rb = loot.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.AddForce(Vector2.up * 3f, ForceMode2D.Impulse);

            ItemPickup pickup = loot.GetComponent<ItemPickup>();
            if (pickup != null) pickup.Setup(selected);
        }
    }

    private T RollWeighted<T>(List<T> list, System.Func<T, float> weightSelector)
    {
        float total = 0f;
        foreach (T item in list) total += weightSelector(item);

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (T item in list)
        {
            cumulative += weightSelector(item);
            if (roll <= cumulative) return item;
        }
        return list[list.Count - 1];
    }
}