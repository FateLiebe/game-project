using UnityEngine;
using UnityEngine.UI;
using TMPro; // Đã thêm thư viện TextMeshPro

public class ItemSlotUI : MonoBehaviour
{
    private Image slotImage; 
    private Button slotButton; 
    private ItemSO currentItem; 
    
    private Sprite defaultSprite; 

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f; 

    [Header("UI Cài đặt thêm")]
    public TextMeshProUGUI quantityText; // Kéo TextMeshPro hiển thị số lượng vào đây

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

    // Đã nâng cấp hàm UpdateSlot để nhận thêm tham số quantity
    public void UpdateSlot(ItemSO item, int quantity = 1)
    {
        currentItem = item;
        
        if (item != null)
        {
            slotImage.sprite = item.icon; 

            // Hiện số lượng nếu có nhiều hơn 1
            if (quantity > 1)
            {
                if (quantityText != null) 
                {
                    quantityText.text = quantity.ToString();
                    quantityText.gameObject.SetActive(true);
                }
            }
            else
            {
                if (quantityText != null) quantityText.gameObject.SetActive(false);
            }
        }
        else
        {
            slotImage.sprite = defaultSprite; 
            if (quantityText != null) quantityText.gameObject.SetActive(false);
        }
    }

    private void OnSlotClicked()
    {
        if (currentItem == null) return;

        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // TRƯỜNG HỢP: DOUBLE CLICK (Mặc đồ / Bơm máu)
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ShowTooltip(currentItem);
                InventoryManager.Instance.UseItem(); 
            }
            
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
        else
        {
            // TRƯỜNG HỢP: SINGLE CLICK (Xem thông tin)
            if (InventoryManager.Instance != null) 
            {
                InventoryManager.Instance.ShowTooltip(currentItem);
            }
        }
    }
}