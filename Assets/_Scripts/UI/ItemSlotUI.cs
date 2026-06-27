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

    private TextMeshProUGUI quantityText; // Tự động tạo bởi script, không cần gán ở Editor
    private TextMeshProUGUI usesText; // Text tạo động để hiển thị số lần dùng
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

        // Tự động tạo Text hiển thị số lần dùng bùa ở góc trên bên trái
        GameObject textObj = new GameObject("UsesText");
        textObj.transform.SetParent(this.transform, false);
        usesText = textObj.AddComponent<TextMeshProUGUI>();
        usesText.alignment = TextAlignmentOptions.TopLeft;
        usesText.fontSize = 28; // Tăng kích thước lên gần gấp đôi
        usesText.color = Color.yellow;
        usesText.fontStyle = FontStyles.Bold;
        usesText.raycastTarget = false;
        
        RectTransform rt = usesText.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(5, -5); // Đẩy nhẹ vào trong để không dính sát viền
        rt.sizeDelta = new Vector2(60, 40); // Mở rộng khung chứa text để không bị cắt chữ
        usesText.gameObject.SetActive(false);

        // Tự động tạo quantityText mới góc dưới bên phải (Màu trắng) nếu chưa có
        if (quantityText == null)
        {
            GameObject qtyObj = new GameObject("QuantityText_Auto");
            qtyObj.transform.SetParent(this.transform, false);
            quantityText = qtyObj.AddComponent<TextMeshProUGUI>();
            quantityText.alignment = TextAlignmentOptions.BottomRight;
            quantityText.fontSize = 28; 
            quantityText.color = Color.white;
            quantityText.fontStyle = FontStyles.Bold;
            quantityText.raycastTarget = false;
            
            RectTransform qrt = quantityText.GetComponent<RectTransform>();
            qrt.anchorMin = new Vector2(1, 0);
            qrt.anchorMax = new Vector2(1, 0);
            qrt.pivot = new Vector2(1, 0);
            qrt.anchoredPosition = new Vector2(-5, 5); 
            qrt.sizeDelta = new Vector2(60, 40);
            quantityText.gameObject.SetActive(false);
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

            // Hiện số lượng (dành cho đồ tiêu hao)
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

            // Hiện số lần dùng còn lại (dành riêng cho Support Skill)
            if (item.itemType == ItemType.SupportSkill)
            {
                if (usesText != null)
                {
                    usesText.text = item.runtimeUses.ToString();
                    usesText.gameObject.SetActive(true);
                }
            }
            else
            {
                if (usesText != null) usesText.gameObject.SetActive(false);
            }
        }
        else
        {
            slotImage.sprite = defaultSprite; 
            if (quantityText != null) quantityText.gameObject.SetActive(false);
            if (usesText != null) usesText.gameObject.SetActive(false);
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