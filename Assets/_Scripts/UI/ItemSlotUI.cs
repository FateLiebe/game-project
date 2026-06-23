using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ô chứa vật phẩm bên trong lưới Inventory.
/// Xử lý cộng dồn số lượng, logic Nhấp 1 lần (Xem Tooltip / Chọn để bán), Nhấp đúp (Sử dụng / Bán ngay lập tức).
/// </summary>
public class ItemSlotUI : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    private Image slotImage; 
    private Button slotButton; 
    private ItemSO currentItem; 
    
    private Sprite defaultSprite; 

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f; 

    [Header("UI Cài đặt thêm")]
    public TextMeshProUGUI quantityText; // Kéo TextMeshPro hiển thị số lượng vào đây
    #endregion

    #region UNITY LIFECYCLE
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
    #endregion

    #region PUBLIC METHODS
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
    #endregion

    #region PRIVATE METHODS
    private void OnSlotClicked()
    {
        if (currentItem == null) return;

        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // DOUBLE CLICK
            if (ShopManager.Instance != null && ShopManager.Instance.isShopOpen)
            {
                ShopManager.Instance.SelectInventoryItemToSell();
                ShopManager.Instance.SellItem(currentItem);
            }
            else if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ShowTooltip(currentItem);
                InventoryManager.Instance.UseItem(); 
            }
            
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
        else
        {
            // SINGLE CLICK
            if (ShopManager.Instance != null && ShopManager.Instance.isShopOpen)
            {
                if (InventoryManager.Instance != null) InventoryManager.Instance.ShowTooltip(currentItem);
                ShopManager.Instance.SelectInventoryItemToSell();
            }
            else if (InventoryManager.Instance != null) 
            {
                InventoryManager.Instance.ShowTooltip(currentItem);
            }
        }
    }
    #endregion
}