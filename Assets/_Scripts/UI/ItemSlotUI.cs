using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    private Image slotImage; 
    private Button slotButton; 
    private ItemSO currentItem; 
    
    private Sprite defaultSprite; 

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f; 

    private void Awake()
    {
        slotImage = GetComponent<Image>();
        slotButton = GetComponent<Button>();
        
        if (slotImage != null) defaultSprite = slotImage.sprite; 

        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    public void UpdateSlot(ItemSO item)
    {
        currentItem = item;
        
        if (item != null)
        {
            slotImage.sprite = item.icon; 
        }
        else
        {
            slotImage.sprite = defaultSprite; 
        }
    }

    private void OnSlotClicked()
    {
        if (currentItem == null) return;

        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // =====================================
            // TRƯỜNG HỢP: DOUBLE CLICK (Mặc đồ / Bơm máu)
            // =====================================
            if (InventoryManager.Instance != null)
            {
                // 1. Phải mượn hàm ShowTooltip để InventoryManager tự động ghi nhớ "selectedItem"
                InventoryManager.Instance.ShowTooltip(currentItem);
                
                // 2. Sau khi đã nhớ, gọi hàm UseItem (hàm này sẽ tự đọc từ selectedItem để dùng)
                InventoryManager.Instance.UseItem(); 
            }
            
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
        else
        {
            // =====================================
            // TRƯỜNG HỢP: SINGLE CLICK (Xem thông tin)
            // =====================================
            // Trả lại logic cũ: Bắt buộc gọi qua InventoryManager để hệ thống ghi nhớ selectedItem
            if (InventoryManager.Instance != null) 
            {
                InventoryManager.Instance.ShowTooltip(currentItem);
            }
        }
    }
}