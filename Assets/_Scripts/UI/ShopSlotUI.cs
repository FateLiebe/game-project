using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện và logic của từng ô chứa vật phẩm riêng lẻ bên trong Shop.
/// Nhận lệnh click chuột (Single/Double click) và báo cáo về ShopManager.
/// </summary>
public class ShopSlotUI : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("UI Components")]
    [Tooltip("Hình ảnh của món đồ hiển thị trên ô")]
    public Image itemIcon;
    [Tooltip("Label hiển thị giá bán của món đồ này")]
    public TextMeshProUGUI txtPrice;
    [Tooltip("Nút bấm bao trọn toàn bộ ô vật phẩm")]
    public Button slotButton;

    // --- BIẾN LƯU TRỮ DỮ LIỆU CỦA Ô NÀY ---
    private ItemSO currentItem;         // Dữ liệu món đồ đang được bày bán
    private int itemPrice;              // Giá tiền đã được ShopManager tính toán ngẫu nhiên
    private ShopManager shopManager;    // Tham chiếu về hệ thống quản lý gốc để báo cáo sự kiện

    // --- HỆ THỐNG PHÁT HIỆN DOUBLE CLICK ---
    private float lastClickTime = 0f;                   // Thời điểm click chuột lần cuối
    private float doubleClickThreshold = 0.25f;         // Thời gian chờ tối đa giữa 2 lần click (250ms)
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        // Gắn sự kiện lắng nghe click chuột vào nút bấm của ô
        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Hàm này được ShopManager gọi khi khởi tạo Cửa hàng để đổ dữ liệu vật phẩm vào ô.
    /// </summary>
    public void SetupSlot(ItemSO item, int price, ShopManager manager)
    {
        currentItem = item;
        itemPrice = price;
        shopManager = manager;

        // Cập nhật ảnh đại diện của món đồ lên giao diện
        if (itemIcon != null && item != null)
        {
            itemIcon.sprite = item.icon;
        }

        // Cập nhật giá bán lên giao diện
        if (txtPrice != null)
        {
            txtPrice.text = price.ToString();
        }
    }
    #endregion

    #region PRIVATE METHODS
    /// <summary>
    /// Xử lý logic khi người chơi click chuột vào ô vật phẩm này.
    /// Bao gồm tính năng Double-Click cực kỳ tiện lợi để mua nhanh mà không cần bấm nút Buy ở giữa.
    /// </summary>
    private void OnSlotClicked()
    {
        if (currentItem == null || shopManager == null) return;

        // Tính toán khoảng thời gian giữa lần bấm này và lần bấm trước
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time; // Cập nhật lại mốc thời gian cho lần bấm tiếp theo

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // DOUBLE CLICK: Khoảng thời gian < 0.25s -> Người chơi click 2 phát cực nhanh
            // Báo cáo lên ShopManager để chọn và sau đó thực hiện thao tác Mua Luôn!
            shopManager.SelectShopItem(currentItem, itemPrice, gameObject);
            shopManager.BuySelectedItem();
        }
        else
        {
            // SINGLE CLICK: Khoảng thời gian dài -> Người chơi chỉ ấn chọn bình thường
            // Báo cáo lên ShopManager để nó hiện thông số ra Tooltip và đổi nút ở giữa thành "Buy"
            shopManager.SelectShopItem(currentItem, itemPrice, gameObject);
        }
    }
    #endregion
}
