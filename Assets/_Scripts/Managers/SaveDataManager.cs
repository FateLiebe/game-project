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
    #region VARIABLES & PROPERTIES
    public static SaveDataManager Instance { get; private set; }
    public GameData currentData;
    public ItemDatabaseSO itemDatabase; 
    private string saveFileName = "saveData_slot1.json";
    #endregion

    #region UNITY LIFECYCLE
    private void Awake() 
    { 
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        } 
        else 
        {
            Destroy(gameObject); 
        }
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Khởi tạo dữ liệu mới hoàn toàn và ghi đè lên file save hiện tại.
    /// </summary>
    public void NewGame() 
    { 
        currentData = new GameData(); 
        SaveGameToFile(); 
    }

    /// <summary>
    /// Ghi dữ liệu từ bộ nhớ đệm (currentData) xuống ổ cứng theo định dạng JSON.
    /// </summary>
    public void SaveGameToFile()
    {
        if (currentData == null) return;
        try 
        { 
            File.WriteAllText(Path.Combine(Application.persistentDataPath, saveFileName), JsonUtility.ToJson(currentData, true)); 
        }
        catch (Exception e) 
        { 
            Debug.LogError("Lỗi Save: " + e.Message); 
        }
    }

    /// <summary>
    /// Đọc dữ liệu từ file JSON trên ổ cứng. Nếu file không tồn tại, tự động tạo mới.
    /// </summary>
    public void LoadGameFromFile()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path)) 
        { 
            currentData = JsonUtility.FromJson<GameData>(File.ReadAllText(path)); 
        }
        else 
        { 
            NewGame(); 
        }
    }

    /// <summary>
    /// Lưu trò chơi tại một Trạm lưu (Checkpoint).
    /// Khác với Save tay, hàm này ghi nhớ riêng biệt vị trí hồi sinh để ném Player về đây khi chết.
    /// </summary>
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
        if (savedSkill != null) 
        {
            // Bước 1: Nạp vào logic của Player (sẽ tạo instance có runtimeUses chính xác)
            player.LoadSupportSkillFromSave(savedSkill, currentData.currentSupportSkillUses);
            // Bước 2: Báo cho InventoryManager cập nhật UI slot SupportSkill (để Inventory window không bị trống)
            inv.LoadEquippedItemFromSave(player.equippedSupportSkill);
        }

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
            if (item != null) 
            {
                for (int i = 0; i < slotData.quantity; i++) 
                {
                    // [FIX] Nếu là Support Skill, Instantiate thành object độc lập để có runtimeUses riêng!
                    if (item.itemType == ItemType.SupportSkill)
                    {
                        ItemSO instance = UnityEngine.Object.Instantiate(item);
                        // Lấy số uses từ save (nếu save cũ chưa có thì dùng maxUses)
                        instance.runtimeUses = (slotData.savedUses > 0) ? slotData.savedUses : item.maxUses;
                        instance.name = item.name;
                        inv.inventoryList.Add(instance);
                    }
                    else
                    {
                        inv.inventoryList.Add(item);
                    }
                }
            }
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
        Vector3 targetPos = Vector3.zero;
        if (GameLoader.currentLoadMode == GameLoader.LoadMode.Respawn)
        {
            targetPos = new Vector3(currentData.checkX, currentData.checkY, currentData.checkZ);
        }
        else 
        {
            targetPos = new Vector3(currentData.posX, currentData.posY, currentData.posZ);
        }

        // Bắn tia Raycast để tìm mặt đất (tránh lỗi Player lơ lửng nếu điểm save nằm hơi cao so với mặt đất)
        RaycastHit2D hit = Physics2D.Raycast(targetPos, Vector2.down, 15f, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol != null)
            {
                float pivotToBottom = player.transform.position.y - playerCol.bounds.min.y;
                targetPos.y = hit.point.y + pivotToBottom + 0.05f;
            }
        }
        player.transform.position = targetPos;
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
                if (item.itemType == ItemType.SupportSkill)
                {
                    // [FIX DỨT ĐIỂM] SupportSkill không được gộp nhóm (stack) để bảo toàn số uses của TỪNG lá bùa
                    currentData.inventoryItems.Add(new InventorySlotData(item.itemID, 1, item.runtimeUses));
                }
                else
                {
                    // Các món đồ tiêu hao khác vẫn gộp nhóm bình thường để tiết kiệm dung lượng save
                    var existing = currentData.inventoryItems.Find(x => x.itemID == item.itemID && x.savedUses == 0);
                    if (existing != null) existing.quantity++;
                    else currentData.inventoryItems.Add(new InventorySlotData(item.itemID, 1, 0));
                }
            }
        }

        // Lưu cài đặt âm thanh
        if (AudioManager.Instance != null)
        {
            currentData.audioMasterVolume = AudioManager.Instance.masterSliderValue;
            currentData.audioIsMuted      = AudioManager.Instance.isMuted;
        }
    }
    #endregion
}