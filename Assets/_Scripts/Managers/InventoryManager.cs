using UnityEngine;
using UnityEngine.UI; 
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Cài đặt Túi đồ")]
    public int maxSlots = 50; 
    public List<ItemSO> inventoryList = new List<ItemSO>(); 

    [Header("Kết nối UI")]
    public Transform slotsParent; 
    public ItemTooltipUI tooltipUI; 
    public EquipmentSlotUI[] equipSlots; 
    
    [Header("Tương tác Item")]
    public Button btnUse; 
    public Button btnDrop; 
    public GameObject droppedItemPrefab; 
    public BaseEntity player; 

    private ItemSlotUI[] slots; 
    private ItemSO selectedItem; 

    // ==========================================
    #region CORE UNITY METHODS
    // ==========================================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (slotsParent != null) slots = slotsParent.GetComponentsInChildren<ItemSlotUI>();

        if (btnUse  != null) btnUse.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); UseItem(); });
        if (btnDrop != null) btnDrop.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); DropItem(); });
    }

    private void Update()
    {
        // [CHẶN] Không xử lý bàn phím khi Pause/GameOver/Victory
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
            return;

        if (Input.GetKeyDown(KeyCode.Q))
            QuickUseConsumable();
    }

    #endregion

    // ==========================================
    #region INVENTORY LOGIC
    // ==========================================

    // ===============================================
    // --- TÍNH TOÁN SỐ Ô THỰC TẾ ĐANG BỊ CHIẾM ---
    // ===============================================
    private int GetOccupiedSlots()
    {
        int occupied = 0;
        HashSet<string> consumableNames = new HashSet<string>();

        foreach (ItemSO item in inventoryList)
        {
            if (item.itemType == ItemType.Consumable)
            {
                if (!consumableNames.Contains(item.itemName))
                {
                    consumableNames.Add(item.itemName);
                    occupied++;
                }
            }
            else 
            {
                occupied++; 
            }
        }
        return occupied;
    }

    public bool AddItem(ItemSO itemToAdd)
    {
        int occupied = GetOccupiedSlots();
        bool isNewConsumable = false;

        // Nếu là đồ tiêu hao, check xem trong túi đã có bình nào trùng tên chưa
        if (itemToAdd.itemType == ItemType.Consumable)
        {
            bool hasAlready = false;
            foreach (var item in inventoryList)
            {
                if (item.itemType == ItemType.Consumable && item.itemName == itemToAdd.itemName)
                {
                    hasAlready = true;
                    break;
                }
            }
            isNewConsumable = !hasAlready;
        }

        // Kiểm tra xem túi đã đầy chưa
        if (itemToAdd.itemType != ItemType.Consumable || isNewConsumable)
        {
            if (occupied >= maxSlots) 
            {
                Debug.Log("<color=red>Túi đồ đã đầy!</color>");
                return false; 
            }
        }

        inventoryList.Add(itemToAdd);
        UpdateUI(); 
        return true; 
    }

    #endregion

    // ==========================================
    #region UI UPDATES
    // ==========================================

    public void UpdateUI()
    {
        if (slots == null) return;

        List<ItemSO> displayList = new List<ItemSO>();
        Dictionary<string, int> consumableCounts = new Dictionary<string, int>();

        foreach (ItemSO item in inventoryList)
        {
            if (item.itemType == ItemType.Consumable)
            {
                if (consumableCounts.ContainsKey(item.itemID))
                    consumableCounts[item.itemID]++;
                else
                {
                    consumableCounts[item.itemID] = 1;
                    displayList.Add(item);
                }
            }
            else displayList.Add(item);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < displayList.Count)
            {
                int quantity = 1;
                if (displayList[i].itemType == ItemType.Consumable)
                    quantity = consumableCounts[displayList[i].itemID];
                slots[i].UpdateSlot(displayList[i], quantity);
            }
            else slots[i].UpdateSlot(null, 0);
        }

        UpdateConsumableQuickSlot();
    }

    private void UpdateConsumableQuickSlot()
    {
        ItemSO firstConsumable = null;
        int count = 0;

        foreach (ItemSO item in inventoryList)
        {
            if (item.itemType == ItemType.Consumable)
            {
                if (firstConsumable == null) firstConsumable = item;
                if (item.itemID == firstConsumable.itemID) count++; // [FIX] dùng itemID
            }
        }

        if (ConsumableUI.Instance != null) ConsumableUI.Instance.UpdateUI(firstConsumable, count);
    }

    public void ShowTooltip(ItemSO item)
    {
        selectedItem = item; 
        if (tooltipUI != null) tooltipUI.ShowTooltip(item);
    }

    #endregion

    // ==========================================
    #region ITEM INTERACTIONS
    // ==========================================

    // ===============================================
    // --- LOGIC PHÍM Q DÙNG NHANH BÌNH MÁU ---
    // ===============================================
    public void QuickUseConsumable()
    {
        ItemSO itemToUse = null;

        foreach (ItemSO item in inventoryList)
        {
            if (item.itemType == ItemType.Consumable)
            {
                itemToUse = item;
                break;
            }
        }

        if (itemToUse != null)
        {
            if (player != null) 
            {
                player.Heal(itemToUse.healAmount);
                // [AUDIO] Phát âm thanh của item (nếu có), fallback sang srcSFX chung
                if (itemToUse.useSound != null)
                    AudioManager.Instance?.PlayDirectClip(itemToUse.useSound);
            }
            inventoryList.Remove(itemToUse);
            UpdateUI(); 
        }
    }

    public void UseItem()
    {
        if (selectedItem == null) return;

        if (selectedItem.itemType == ItemType.Consumable)
        {
            if (player != null) player.Heal(selectedItem.healAmount);
            // [AUDIO] Phát âm thanh của item
            if (selectedItem.useSound != null)
                AudioManager.Instance?.PlayDirectClip(selectedItem.useSound);
            RemoveSelectedItem();
        }
        else 
        {
            EquipItem(selectedItem);
        }
    }

    private void EquipItem(ItemSO itemToEquip)
    {
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.allowedItemType == itemToEquip.itemType)
            {
                ItemSO previousItem = slot.equippedItem;

                slot.UpdateSlot(itemToEquip);
                inventoryList.Remove(itemToEquip);

                if (previousItem != null) inventoryList.Add(previousItem);

                selectedItem = null;
                if (tooltipUI != null) tooltipUI.HideTooltip();
                UpdateUI();
                RecalculatePlayerStats();
                AudioManager.Instance?.PlayEquip(); // [AUDIO]

                if (itemToEquip.itemType == ItemType.SupportSkill && player is PlayerController pc)
                {
                    pc.EquipSupportSkill(itemToEquip);
                }

                Debug.Log($"<color=cyan>Đã mặc: {itemToEquip.itemName}</color>");
                return; 
            }
        }
    }

    #endregion

    // ==========================================
    #region EQUIPMENT LOGIC
    // ==========================================

    private void RecalculatePlayerStats()
    {
        if (player == null) return;

        float bonusHP = 0, bonusATK = 0, bonusDEF = 0, bonusCRIT = 0;
        float bonusCritDmg = 0, bonusSpeed = 0; 

        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.equippedItem != null)
            {
                bonusHP += slot.equippedItem.healthBonus;
                bonusATK += slot.equippedItem.attackBonus;
                bonusDEF += slot.equippedItem.defenseBonus;
                bonusCRIT += slot.equippedItem.critRateBonus;
                bonusCritDmg += slot.equippedItem.critDamageBonus;
                bonusSpeed += slot.equippedItem.speedBonus;
            }
        }

        player.UpdateEquipmentStats(bonusHP, bonusATK, bonusDEF, bonusCRIT, bonusCritDmg, bonusSpeed);
    }

    #endregion

    // ==========================================
    #region ITEM DROP & UNEQUIP
    // ==========================================

    public void DropItem()
    {
        if (selectedItem == null) return;
        if (droppedItemPrefab == null || player == null) return;

        Vector3 dropOffset = new Vector3(player.transform.localScale.x > 0 ? 1f : -1f, 0.5f, 0);
        GameObject loot;
        if (ObjectPoolManager.Instance != null) loot = ObjectPoolManager.Instance.Get(droppedItemPrefab, player.transform.position + dropOffset, Quaternion.identity);
        else loot = Instantiate(droppedItemPrefab, player.transform.position + dropOffset, Quaternion.identity);
        
        ItemPickup pickup = loot.GetComponent<ItemPickup>();
        if (pickup != null) pickup.Setup(selectedItem, true);
        
        Rigidbody2D rb = loot.GetComponent<Rigidbody2D>();
        if (rb != null) rb.AddForce(new Vector2(Random.Range(-2f, 2f), 3f), ForceMode2D.Impulse);

        RemoveSelectedItem();
    }

    private void RemoveSelectedItem()
    {
        inventoryList.Remove(selectedItem);
        selectedItem = null;
        if (tooltipUI != null) tooltipUI.HideTooltip(); 
        UpdateUI(); 
    }

    public void UnequipItem(EquipmentSlotUI slot)
    {
        if (slot.equippedItem == null) return;

        if (GetOccupiedSlots() >= maxSlots)
        {
            Debug.Log("<color=red>Túi đồ đã đầy, không thể tháo trang bị!</color>");
            return;
        }

        inventoryList.Add(slot.equippedItem);
        AudioManager.Instance?.PlayUnequip(); // [AUDIO]
        
        if (slot.allowedItemType == ItemType.SupportSkill && player is PlayerController pc)
        {
            pc.EquipSupportSkill(null);
            if (SupportSkillUI.Instance != null) SupportSkillUI.Instance.UpdateUI(null, 0, 0);
        }

        slot.UpdateSlot(null);
        UpdateUI();
        RecalculatePlayerStats();
        if (tooltipUI != null) tooltipUI.HideTooltip();
        Debug.Log("<color=yellow>Đã tháo trang bị xuống túi.</color>");
    }

    public void RemoveBrokenEquipment(ItemType type)
    {
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.allowedItemType == type && slot.equippedItem != null)
            {
                slot.UpdateSlot(null); 
                RecalculatePlayerStats();
                break;
            }
        }
    }

    #endregion

    // ==========================================
    #region SAVE & LOAD
    // ==========================================

    public void LoadEquippedItemFromSave(ItemSO item)
    {
        if (item == null) return;
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.allowedItemType == item.itemType) { slot.UpdateSlot(item); break; }
        }
        RecalculatePlayerStats();
    }

    #endregion
}