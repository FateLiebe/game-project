using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ô Trang bị đang mặc.
/// Cho phép Nhấp 1 lần (Single-click) để xem thông tin, Nhấp đúp (Double-click) để cởi đồ bỏ lại vào Túi.
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("Quy định loại trang bị")]
    public ItemType allowedItemType; // Ô này chỉ được chứa loại đồ gì?

    [HideInInspector] public ItemSO equippedItem; // Món đồ đang mặc
    
    private Image slotImage;
    private Button slotButton;
    private Sprite defaultSprite; // Ảnh trống lúc chưa mặc đồ
    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        slotImage = GetComponent<Image>();
        slotButton = GetComponent<Button>();

        if (slotImage != null) defaultSprite = slotImage.sprite;

        // Cho phép bấm vào đồ đang mặc để xem Tooltip
        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }
    #endregion

    #region PUBLIC METHODS
    public void UpdateSlot(ItemSO item)
    {
        equippedItem = item;
        
        if (item != null)
        {
            slotImage.sprite = item.icon; // Đổi thành ảnh trang bị
        }
        else
        {
            slotImage.sprite = defaultSprite; // Trả về ảnh trống ban đầu
        }
    }
    #endregion

    #region PRIVATE METHODS
    private void OnSlotClicked()
    {
        if (equippedItem == null) return;

        // Tính toán khoảng thời gian giữa 2 lần click liên tiếp
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // TRƯỜNG HỢP: DOUBLE CLICK (Cởi đồ)
            if (InventoryManager.Instance != null) InventoryManager.Instance.UnequipItem(this);
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
        else
        {
            // TRƯỜNG HỢP: SINGLE CLICK (Chỉ xem)
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.ShowTooltip(equippedItem);
        }
    }
    #endregion
}