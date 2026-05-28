using UnityEngine;

// Các loại trang bị khớp với Ảnh 1 của bạn
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
}