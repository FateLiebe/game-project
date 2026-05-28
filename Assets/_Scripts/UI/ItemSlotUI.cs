using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    private Image slotImage; // Ảnh của chính cái nút
    private Button slotButton; // Nút bấm
    private ItemSO currentItem; // Dữ liệu món đồ trong ô này
    
    private Sprite defaultSprite; // Nhớ lại ảnh nền trắng mặc định

    private void Awake()
    {
        slotImage = GetComponent<Image>();
        slotButton = GetComponent<Button>();
        
        // Lưu lại cái ảnh trắng bóc ban đầu để dùng khi vứt đồ đi
        if (slotImage != null) defaultSprite = slotImage.sprite; 

        // Tự động gán sự kiện khi bạn click vào ô này
        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    // Hàm này được gọi bởi InventoryManager để cập nhật ảnh
    public void UpdateSlot(ItemSO item)
    {
        currentItem = item;
        
        if (item != null)
        {
            slotImage.sprite = item.icon; // Có đồ -> Hiện ảnh kiếm, máu...
        }
        else
        {
            slotImage.sprite = defaultSprite; // Trống -> Trả về ảnh trắng
        }
    }

    // Khi click chuột vào ô
    private void OnSlotClicked()
    {
        if (currentItem != null && InventoryManager.Instance != null)
        {
            // Báo cho Manager bật cái bảng Tooltip lên
            InventoryManager.Instance.ShowTooltip(currentItem);
        }
    }
}