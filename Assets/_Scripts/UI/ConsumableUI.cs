using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConsumableUI : MonoBehaviour
{
    public static ConsumableUI Instance;

    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    private void Awake() 
    { 
        Instance = this; 
        // Vừa vào game thì tự động ẩn đi
        UpdateUI(null, 0); 
    }

    // Hàm này sẽ được InventoryManager gọi liên tục mỗi khi túi đồ có sự thay đổi
    public void UpdateUI(ItemSO consumableItem, int totalQuantity)
    {
        // Nếu không có bình máu nào trong túi hoặc số lượng = 0 -> Ẩn UI
        if (consumableItem == null || totalQuantity <= 0)
        {
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (quantityText != null) quantityText.gameObject.SetActive(false);
            return;
        }

        // Nếu có thì hiện UI lên và cập nhật ảnh + số lượng
        if (iconImage != null) iconImage.gameObject.SetActive(true);
        if (quantityText != null) quantityText.gameObject.SetActive(true);

        iconImage.sprite = consumableItem.icon;
        quantityText.text = totalQuantity.ToString();
    }
}