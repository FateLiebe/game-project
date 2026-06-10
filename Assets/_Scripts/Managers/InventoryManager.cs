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
    public EquipmentSlotUI[] equipSlots; // 7 Ô trang bị
    
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

    public bool AddItem(ItemSO itemToAdd)
    {
        if (inventoryList.Count >= maxSlots) return false;
        inventoryList.Add(itemToAdd);
        UpdateUI(); 
        return true; 
    }

    private void UpdateUI()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < inventoryList.Count) slots[i].UpdateSlot(inventoryList[i]);
            else slots[i].UpdateSlot(null);
        }
    }

    public void ShowTooltip(ItemSO item)
    {
        selectedItem = item; 
        if (tooltipUI != null) tooltipUI.ShowTooltip(item);
    }

    // ===============================================
    // --- LOGIC SỬ DỤNG / MẶC ĐỒ ---
    // ===============================================
    public void UseItem()
    {
        if (selectedItem == null) return;

        if (selectedItem.itemType == ItemType.Consumable)
        {
            if (player != null) player.Heal(selectedItem.healAmount);
            RemoveSelectedItem();
        }
        else // Nếu là Trang bị (Vũ khí, Áo, Mũ...)
        {
            EquipItem(selectedItem);
        }
    }

    private void EquipItem(ItemSO itemToEquip)
    {
        // Quét 7 ô trang bị xem ô nào đúng loại đồ này
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.allowedItemType == itemToEquip.itemType)
            {
                // Nhớ lại xem ô này đang mặc cái gì cũ không
                ItemSO previousItem = slot.equippedItem;

                // Mặc đồ mới lên
                slot.UpdateSlot(itemToEquip);
                
                // Xóa đồ mới khỏi túi
                inventoryList.Remove(itemToEquip);

                // TRÁO ĐỒ: Nếu có đồ cũ, ném nó ngược lại vào túi
                if (previousItem != null)
                {
                    inventoryList.Add(previousItem);
                }

                // Cập nhật giao diện và Chỉ số
                selectedItem = null;
                if (tooltipUI != null) tooltipUI.HideTooltip();
                UpdateUI();
                RecalculatePlayerStats();

                Debug.Log($"<color=cyan>Đã mặc: {itemToEquip.itemName}</color>");
                return; // Xong việc thì thoát vòng lặp
            }
        }
    }

    // ===============================================
    // --- LOGIC TÍNH TOÁN SỨC MẠNH TỔNG ---
    // ===============================================
    private void RecalculatePlayerStats()
    {
        if (player == null) return;

        float bonusHP = 0, bonusATK = 0, bonusDEF = 0, bonusCRIT = 0;
        float bonusCritDmg = 0, bonusSpeed = 0; // [MỚI THÊM]: Sát thương bạo kích và Tốc độ

        // Cộng dồn sức mạnh của cả 7 món trên người
        foreach (EquipmentSlotUI slot in equipSlots)
        {
            if (slot.equippedItem != null)
            {
                bonusHP += slot.equippedItem.healthBonus;
                bonusATK += slot.equippedItem.attackBonus;
                bonusDEF += slot.equippedItem.defenseBonus;
                bonusCRIT += slot.equippedItem.critRateBonus;

                // [MỚI THÊM]: Lấy chỉ số mới
                bonusCritDmg += slot.equippedItem.critDamageBonus;
                bonusSpeed += slot.equippedItem.speedBonus;
            }
        }

        // [ĐÃ SỬA]: Truyền cả 6 chỉ số sang BaseEntity
        player.UpdateEquipmentStats(bonusHP, bonusATK, bonusDEF, bonusCRIT, bonusCritDmg, bonusSpeed);
    }

    // ===============================================
    // --- LOGIC VỨT ĐỒ ---
    // ===============================================
    public void DropItem()
    {
        if (selectedItem == null) { Debug.Log("selectedItem NULL"); return; }
        if (droppedItemPrefab == null) { Debug.Log("droppedItemPrefab NULL - chưa gán prefab!"); return; }
        if (player == null) { Debug.Log("player NULL - chưa gán player!"); return; }

        Vector3 dropOffset = new Vector3(
            player.transform.localScale.x > 0 ? 1f : -1f, 
            0.5f, 0);

        GameObject loot = Instantiate(droppedItemPrefab, 
            player.transform.position + dropOffset, 
            Quaternion.identity);
        
        Debug.Log($"Spawned: {loot.name} tại {loot.transform.position}");
        
        ItemPickup pickup = loot.GetComponent<ItemPickup>();
        if (pickup == null) { Debug.Log("Prefab thiếu ItemPickup component!"); return; }
        
        pickup.Setup(selectedItem);
        
        Rigidbody2D rb = loot.GetComponent<Rigidbody2D>();
        if (rb == null) Debug.Log("Prefab thiếu Rigidbody2D!");
        else rb.AddForce(new Vector2(Random.Range(-2f, 2f), 3f), ForceMode2D.Impulse);

        RemoveSelectedItem();
    }

    private void RemoveSelectedItem()
    {
        inventoryList.Remove(selectedItem);
        selectedItem = null;
        if (tooltipUI != null) tooltipUI.HideTooltip(); 
        UpdateUI(); 
    }

    // ===============================================
    // --- LOGIC THÁO ĐỒ TỪ NGƯỜI XUỐNG TÚI ---
    // ===============================================
    public void UnequipItem(EquipmentSlotUI slot)
    {
        if (slot.equippedItem == null) return;

        // 1. Kiểm tra xem túi có bị đầy không
        if (inventoryList.Count >= maxSlots)
        {
            Debug.Log("<color=red>Túi đồ đã đầy, không thể tháo trang bị!</color>");
            return;
        }

        // 2. Ném đồ từ người trở lại vào list túi đồ
        inventoryList.Add(slot.equippedItem);

        // 3. Xóa ảnh món đồ trên ô trang bị (trả về ảnh trống)
        slot.UpdateSlot(null);

        // 4. Cập nhật lại giao diện túi và TRỪ CHỈ SỐ trên người
        UpdateUI();
        RecalculatePlayerStats();
        
        if (tooltipUI != null) tooltipUI.HideTooltip();
        Debug.Log("<color=yellow>Đã tháo trang bị xuống túi.</color>");
    }
}