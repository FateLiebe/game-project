using UnityEngine;

public enum ItemType 
{ 
    Consumable,     // Đồ tiêu hao (Bình máu, v.v.)
    Helmet,         // Mũ
    Weapon,         // Vũ khí
    Armor,          // Áo
    Pants,          // Quần
    Boots,          // Giày
    Accessory,      // Bảo vật
    SupportSkill    // Kỹ năng hỗ trợ
}

public enum ItemRarity
{
    Common,     // Thường (Trắng)
    Rare,       // Hiếm (Xanh)
    Epic,       // Tinh Anh (Tím)
    Legendary   // Huyền Thoại (Vàng)
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Data/Item")]
public class ItemSO : ScriptableObject
{

    [Header("System")]
    [Tooltip("ID duy nhất để lưu Save/Load (VD: wp_sword_01)")]
    public string itemID;

    [Header("Thông tin cơ bản")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon; // Ảnh hiển thị trong túi đồ
    public ItemType itemType;
    public ItemRarity rarity;

    [Header("Chỉ số cộng thêm (Dành cho trang bị)")]
    public int healthBonus;
    public int attackBonus;
    public int defenseBonus;
    public float critRateBonus;
    public float critDamageBonus;
    public float speedBonus;

    [Header("Giá trị sử dụng (Dành cho đồ tiêu hao)")]
    public int healAmount;

    [Header("--- SUPPORT SKILL SETTINGS (Dành cho Bùa) ---")]
    [Tooltip("Prefab VFX/Đạn bắn ra (Bắt buộc phải gắn UniversalHitbox)")]
    public GameObject skillPrefab;
        
    [Tooltip("Hệ số sát thương (VD: 1.5 = 150% ATK của Player)")]
    public float damageMultiplier = 1.5f;
        
    [Tooltip("Thời gian hồi chiêu (Giây)")]
    public float skillCooldown = 5f;
        
    [Tooltip("Số lần sử dụng tối đa")]
    public int maxUses = 3;
}