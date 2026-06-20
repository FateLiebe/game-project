using UnityEngine;
using System.IO;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// Trái tim của hệ thống Lưu/Tải (Save/Load).
/// Gói toàn bộ dữ liệu trạng thái hiện tại thành chuỗi JSON và ghi xuống ổ cứng.
/// </summary>
public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance { get; private set; }
    public GameData currentData;
    public ItemDatabaseSO itemDatabase; 
    private string saveFileName = "saveData_slot1.json";

    private void Awake() { if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); } else Destroy(gameObject); }

    public void NewGame() { currentData = new GameData(); SaveGameToFile(); }

    public void SaveGameToFile()
    {
        if (currentData == null) return;
        try { File.WriteAllText(Path.Combine(Application.persistentDataPath, saveFileName), JsonUtility.ToJson(currentData, true)); }
        catch (Exception e) { Debug.LogError("Lỗi Save: " + e.Message); }
    }

    public void LoadGameFromFile()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path)) { currentData = JsonUtility.FromJson<GameData>(File.ReadAllText(path)); }
        else { NewGame(); }
    }

    /// <summary>
    /// Quét vòng quanh người chơi và túi đồ để thu thập toàn bộ: Máu, Level, Vàng, Đồ mặc, Đồ trong túi, Vị trí.
    /// Hàm này được gọi ngay trước khi Save xuống ổ cứng.
    /// </summary>
    public void CollectDataFromGame(PlayerController player, InventoryManager inv, bool savePosition = true)
    {
        if (currentData == null) return;
        
        // 1. Chỉ số
        currentData.currentHealth = player.currentHealth; currentData.currentLevel = player.currentLevel;
        currentData.currentEXP = player.currentEXP; currentData.expToNextLevel = player.expToNextLevel;
        currentData.currentStatPoints = player.currentStatPoints; currentData.addedHealthPoints = player.addedHealthPoints;
        currentData.addedAttackPoints = player.addedAttackPoints; currentData.addedDefensePoints = player.addedDefensePoints;
        currentData.addedCritPoints = player.addedCritPoints;
        
        // 2. Tên Map
        currentData.currentSceneName = "Map_1"; 
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "Core_Gameplay" && s.name != "Main_Menu" && s.name != "DontDestroyOnLoad")
            {
                currentData.currentSceneName = s.name;
                break;
            }
        }
        
        // 3. Vị trí Lưu Tay (Lưu chính xác tọa độ Player đang đứng)
        if (savePosition)
        {
            currentData.posX = player.transform.position.x;
            currentData.posY = player.transform.position.y;
            currentData.posZ = player.transform.position.z;
        }

        // 4. Trang bị & Đồ đạc
        foreach (EquipmentSlotUI slot in inv.equipSlots)
        {
            string idToSave = (slot.equippedItem != null) ? slot.equippedItem.itemID : "";
            switch (slot.allowedItemType)
            {
                case ItemType.Weapon: currentData.weaponID = idToSave; break;
                case ItemType.Helmet: currentData.helmetID = idToSave; break;
                case ItemType.Armor: currentData.armorID = idToSave; break;
                case ItemType.Pants: currentData.pantsID = idToSave; break;
                case ItemType.Boots: currentData.bootsID = idToSave; break;
                case ItemType.Accessory: currentData.accessoryID = idToSave; break;
            }
        }
        currentData.equippedSupportSkillID = player.equippedSupportSkill != null ? player.equippedSupportSkill.itemID : "";
        currentData.currentSupportSkillUses = player.currentSupportSkillUses;

        currentData.coins = player.coins;

        currentData.inventoryItems.Clear();
        foreach (ItemSO item in inv.inventoryList) 
        {
            if (item != null)
            {
                var existing = currentData.inventoryItems.Find(x => x.itemID == item.itemID);
                if (existing != null) existing.quantity++;
                else currentData.inventoryItems.Add(new InventorySlotData(item.itemID, 1));
            }
        }

        // Lưu cài đặt âm thanh
        if (AudioManager.Instance != null)
        {
            currentData.audioMasterVolume = AudioManager.Instance.masterSliderValue;
            currentData.audioIsMuted      = AudioManager.Instance.isMuted;
        }
    }

    // Dành riêng cho việc chạm vào Trạm Lưu (Auto-Save)
    public void SaveAtCheckpoint(PlayerController player, InventoryManager inv, Transform cpTransform)
    {
        if (currentData == null) return;

        // 1. Gom toàn bộ thông số Máu, Cấp, Đồ đạc hiện tại
        CollectDataFromGame(player, inv, false);

        // 2. Ghi đè tọa độ "Continue" thành tọa độ Trạm 
        currentData.posX = cpTransform.position.x;
        currentData.posY = cpTransform.position.y;
        currentData.posZ = cpTransform.position.z;

        // 3. Luôn ghi chốt tọa độ "Hồi Sinh" = trạm vừa chạm (trạm cuối cùng gặp)
        currentData.checkSceneName = currentData.currentSceneName; // Scene hiện tại
        currentData.checkX = cpTransform.position.x;
        currentData.checkY = cpTransform.position.y;
        currentData.checkZ = cpTransform.position.z;

        // 4. Lưu trực tiếp xuống ổ cứng
        SaveGameToFile();
    }

    /// <summary>
    /// Đọc dữ liệu từ ổ cứng lên và "tiêm" (inject) vào Player.
    /// Phân luồng đặc biệt: Nếu vừa chết xong thì Bơm Đầy Máu và Ném về trạm lưu (Checkpoint).
    /// </summary>
    public void ApplyLoadedDataToPlayer(PlayerController player, InventoryManager inv)
    {
        if (currentData == null || itemDatabase == null) return;

        // Đánh thức Player
        player.Revive();

        // Nạp chỉ số
        player.currentLevel = currentData.currentLevel; player.currentEXP = currentData.currentEXP; player.expToNextLevel = currentData.expToNextLevel;
        player.currentStatPoints = currentData.currentStatPoints; player.addedHealthPoints = currentData.addedHealthPoints;
        player.addedAttackPoints = currentData.addedAttackPoints; player.addedDefensePoints = currentData.addedDefensePoints; player.addedCritPoints = currentData.addedCritPoints;

        // Nạp trang bị
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.weaponID));
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.helmetID));
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.armorID));
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.pantsID));
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.bootsID));
        inv.LoadEquippedItemFromSave(itemDatabase.GetItemByID(currentData.accessoryID));

        ItemSO savedSkill = itemDatabase.GetItemByID(currentData.equippedSupportSkillID);
        if (savedSkill != null) player.LoadSupportSkillFromSave(savedSkill, currentData.currentSupportSkillUses);

        // Nạp tiền
        player.coins = currentData.coins;

        // ==========================================
        // PHÂN LUỒNG BƠM MÁU
        // ==========================================
        if (GameLoader.currentLoadMode == GameLoader.LoadMode.Respawn)
        {
            player.currentHealth = player.MaxHealth; // Bơm đầy 100% nếu vừa chết xong
        }
        else
        {
            player.currentHealth = Mathf.Clamp(currentData.currentHealth, 0f, player.MaxHealth); // Giữ máu cũ nếu Load bình thường
        }

        // Cập nhật lên UI
        player.RefreshUIAfterLoad();
        if (UIManager.Instance != null && UIManager.Instance.hpSlider != null)
        {
            UIManager.Instance.hpSlider.value = player.currentHealth / player.MaxHealth;
        }

        // Nạp hòm đồ
        inv.inventoryList.Clear();
        foreach (InventorySlotData slotData in currentData.inventoryItems)
        {
            ItemSO item = itemDatabase.GetItemByID(slotData.itemID);
            if (item != null) for (int i = 0; i < slotData.quantity; i++) inv.inventoryList.Add(item);
        }
        inv.UpdateUI(); 

        // Nạp cài đặt âm thanh và đồng bộ UI
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.LoadSettings(currentData.audioMasterVolume, currentData.audioIsMuted);
            UIManager.Instance?.SyncAudioUI(); // Cập nhật Slider/Toggle trên PauseScreen
        }

        // ==========================================
        // PHÂN LUỒNG TỌA ĐỘ BẮT ĐẦU
        // ==========================================
        if (GameLoader.currentLoadMode == GameLoader.LoadMode.Respawn)
        {
            // Bị chết: Ném về Trạm Hồi Sinh
            player.transform.position = new Vector3(currentData.checkX, currentData.checkY, currentData.checkZ);
        }
        else 
        {
            // Continue bình thường: Ném ra đúng chỗ vừa Save tay (Hoặc Trạm gần nhất nếu chưa Save tay bao giờ)
            player.transform.position = new Vector3(currentData.posX, currentData.posY, currentData.posZ);
        }
    }
}