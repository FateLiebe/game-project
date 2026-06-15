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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (slotsParent != null) slots = slotsParent.GetComponentsInChildren<ItemSlotUI>();

        if (btnUse != null) btnUse.onClick.AddListener(UseItem);
        if (btnDrop != null) btnDrop.onClick.AddListener(DropItem);
    }

    private void Update()
    {
        // Lắng nghe phím Q để uống bình máu
        if (Input.GetKeyDown(KeyCode.Q))
        {
            QuickUseConsumable();
        }
    }

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

    public void UpdateUI()
    {
        if (slots == null) return;

        List<ItemSO> displayList = new List<ItemSO>();
        Dictionary<string, int> consumableCounts = new Dictionary<string, int>();

        // Phân loại và gom nhóm
        foreach (ItemSO item in inventoryList)
        {
            if (item.itemType == ItemType.Consumable)
            {
                if (consumableCounts.ContainsKey(item.itemName))
                {
                    consumableCounts[item.itemName]++;
                }
                else
                {
                    consumableCounts[item.itemName] = 1;
                    displayList.Add(item); 
                }
            }
            else
            {
                displayList.Add(item);
            }
        }

        // Đổ dữ liệu ra các ô
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < displayList.Count)
            {
                int quantity = 1;
                if (displayList[i].itemType == ItemType.Consumable)
                {
                    quantity = consumableCounts[displayList[i].itemName];
                }
                
                slots[i].UpdateSlot(displayList[i], quantity);
            }
            else 
            {
                slots[i].UpdateSlot(null, 0);
            }
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
                if (item.itemName == firstConsumable.itemName) count++;
            }
        }

        if (ConsumableUI.Instance != null) ConsumableUI.Instance.UpdateUI(firstConsumable, count);
    }

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
                Debug.Log($"<color=green>Đã dùng {itemToUse.itemName}, hồi {itemToUse.healAmount} HP.</color>");
            }
            inventoryList.Remove(itemToUse);
            UpdateUI(); 
        }
    }

    public void ShowTooltip(ItemSO item)
    {
        selectedItem = item; 
        if (tooltipUI != null) tooltipUI.ShowTooltip(item);
    }

    public void UseItem()
    {
        if (selectedItem == null) return;

        if (selectedItem.itemType == ItemType.Consumable)
        {
            if (player != null) player.Heal(selectedItem.healAmount);
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

                if (itemToEquip.itemType == ItemType.SupportSkill && player is PlayerController pc)
                {
                    pc.EquipSupportSkill(itemToEquip);
                }

                Debug.Log($"<color=cyan>Đã mặc: {itemToEquip.itemName}</color>");
                return; 
            }
        }
    }

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

    public void DropItem()
    {
        if (selectedItem == null) return;
        if (droppedItemPrefab == null || player == null) return;

        Vector3 dropOffset = new Vector3(player.transform.localScale.x > 0 ? 1f : -1f, 0.5f, 0);
        GameObject loot = Instantiate(droppedItemPrefab, player.transform.position + dropOffset, Quaternion.identity);
        
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

    public void LoadEquippedItemFromSave(ItemSO item)
    {
        if (item == null) return;
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.allowedItemType == item.itemType) { slot.UpdateSlot(item); break; }
        }
        RecalculatePlayerStats();
    }
}