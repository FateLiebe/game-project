using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu trúc dữ liệu dùng để nén (Serialize) và ghi ra file JSON.
/// Lưu trữ toàn bộ thông tin về chỉ số, tọa độ hiện tại, trang bị đang mặc và danh sách vật phẩm trong túi đồ.
/// </summary>

#region DATA CLASSES
[System.Serializable]
public class InventorySlotData
{
    public string itemID;
    public int quantity;
    public int savedUses; // Lưu runtimeUses của SupportSkill
    
    public InventorySlotData(string id, int amount, int uses = 0) 
    { 
        itemID = id; 
        quantity = amount; 
        savedUses = uses;
    }
}

[System.Serializable]
public class GameData
{
    public float currentHealth; public int currentLevel; public float currentEXP; public float expToNextLevel; 
    public int currentStatPoints; public int addedHealthPoints; public int addedAttackPoints; public int addedDefensePoints; public int addedCritPoints;     
    public string currentSceneName; public float posX; public float posY; public float posZ;
    public string weaponID; public string helmetID; public string armorID; public string pantsID; public string bootsID; public string accessoryID;
    public string equippedSupportSkillID; public int currentSupportSkillUses;   
    public List<InventorySlotData> inventoryItems; 

    public string checkSceneName = "Map_1";
    public float checkX;
    public float checkY;
    public float checkZ;
    public int coins = 0; // Thêm biến lưu số Vàng (Coin) của người chơi

    // ÂM THANH
    public float audioMasterVolume = 10f; // 1-10
    public bool  audioIsMuted      = false;

    #region CONSTRUCTOR
    public GameData()
    {
        currentHealth = 30f; currentLevel = 1; currentEXP = 0f; expToNextLevel = 30f; 
        currentStatPoints = 0; addedHealthPoints = 0; addedAttackPoints = 0; addedDefensePoints = 0; addedCritPoints = 0;
        currentSceneName = "Map_1"; posX = 0f; posY = 0f; posZ = 0f;
        weaponID = ""; helmetID = ""; armorID = ""; pantsID = ""; bootsID = ""; accessoryID = ""; equippedSupportSkillID = ""; currentSupportSkillUses = 0;
        inventoryItems = new List<InventorySlotData>();
    }
    #endregion
}
#endregion