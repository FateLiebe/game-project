using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Quản lý giao diện Bảng thông tin chi tiết (Tooltip) hiện lên khi người chơi click hoặc trỏ chuột vào vật phẩm.
/// Class này được cấu hình thành Singleton để bất kỳ ai (Túi đồ, Shop) cũng có thể gọi nó lên dễ dàng.
/// </summary>
public class ItemTooltipUI : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static ItemTooltipUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Panel gốc chứa toàn bộ giao diện Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI txtItemName;
    [SerializeField] private TextMeshProUGUI txtItemType;
    [SerializeField] private TextMeshProUGUI txtStats;
    [Tooltip("Label hiển thị Giá tiền (có thể là giá gốc hoặc giá Shop)")]
    [SerializeField] private TextMeshProUGUI txtItemPrice; 

    // --- BIẾN TOẠ ĐỘ VÀ KÍCH THƯỚC ---
    private RectTransform rectTransform; // Transform của chính cái Tooltip
    private RectTransform parentRect;    // Transform của Canvas chứa Tooltip này

    private ItemSO currentItem;          // Vật phẩm đang được xem hiện tại

    // --- HIỆU ỨNG NHẤP NHÁY ---
    private Color originalNameColor;     // Màu gốc của Tên vật phẩm (Dựa trên độ hiếm)
    private float baseBlinkSpeed = 2f;   // Tốc độ nhấp nháy cơ bản

    private Canvas parentCanvas;
    private Camera uiCamera;

    // Cờ báo hiệu: Chỉ cho phép click chuột để tắt Tooltip sau khi nó đã hiện được 1 frame
    // Tránh tình trạng người chơi vừa click mở Tooltip lên thì nó tự hiểu là click tắt luôn.
    private bool canCloseTooltip;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        // Thiết lập Singleton
        if (Instance == null)
            Instance = this;

        if (tooltipPanel != null)
        {
            rectTransform = tooltipPanel.GetComponent<RectTransform>();

            if (rectTransform != null)
            {
                // Pivot (0, 1) nghĩa là lấy GÓC TRÊN CÙNG BÊN TRÁI của Tooltip làm mốc tính tọa độ
                rectTransform.pivot = new Vector2(0f, 1f);

                // Cache lại Transform của Canvas chứa nó để tính toán mép màn hình sau này
                parentRect = rectTransform.parent as RectTransform;
            }

            // Tự động tìm kiếm chữ TXT_ItemPrice bên trong nếu developer lỡ quên kéo thả vào Inspector
            if (txtItemPrice == null)
            {
                Transform priceTrans = tooltipPanel.transform.Find("TXT_ItemPrice");
                if (priceTrans != null) txtItemPrice = priceTrans.GetComponent<TextMeshProUGUI>();
            }
        }

        // Tìm Canvas cao nhất, kể cả khi Tooltip đang bị ẩn (SetActive(false))
        parentCanvas = GetComponentInParent<Canvas>(true);

        // Nếu Canvas xài Camera (World Space / Screen Space Camera), lấy Camera đó ra để quy đổi tọa độ chuột
        if (parentCanvas != null &&
            parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        HideTooltip(); // Đảm bảo Tooltip luôn tắt khi vừa vào game
    }

    private void Update()
    {
        if (tooltipPanel == null || !tooltipPanel.activeSelf)
            return;

        // TÍNH NĂNG: Click chuột ra ngoài khoảng không để tắt Tooltip
        if (canCloseTooltip && Input.GetMouseButtonDown(0))
        {
            // Kiểm tra xem vị trí chuột hiện tại CÓ NẰM TRONG khung hình chữ nhật của Tooltip không?
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform,
                Input.mousePosition,
                uiCamera))
            {
                HideTooltip(); // Nếu nằm ngoài -> Tắt Tooltip
                return;
            }
        }

        // HIỆU ỨNG: Chữ tên vật phẩm nhấp nháy mờ ảo dựa theo độ hiếm
        // (Đồ càng hiếm chớp càng nhanh)
        if (currentItem != null && txtItemName != null)
        {
            float rarityMultiplier = (int)currentItem.rarity + 1f;
            float currentSpeed = baseBlinkSpeed * rarityMultiplier;

            // Dùng PingPong để chạy giá trị Alpha (độ mờ) từ 0.3 lên 1.0 rồi lặp lại
            float alpha = Mathf.Lerp(
                0.3f,
                1f,
                Mathf.PingPong(Time.time * currentSpeed, 1f));

            txtItemName.color = new Color(
                originalNameColor.r,
                originalNameColor.g,
                originalNameColor.b,
                alpha);
        }
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Hiển thị Tooltip. Nếu customPrice = -1 thì lấy giá gốc, nếu truyền vào thì lấy giá Shop.
    /// </summary>
    public void ShowTooltip(ItemSO item, int customPrice = -1)
    {
        if (item == null || tooltipPanel == null)
            return;

        currentItem = item;

        // ===== 1. CẬP NHẬT TÊN =====
        if (txtItemName != null)
        {
            txtItemName.text = item.itemName;
            originalNameColor = GetRarityColor((int)item.rarity);
            txtItemName.color = originalNameColor;
        }

        // ===== 2. CẬP NHẬT LOẠI VẬT PHẨM =====
        if (txtItemType != null)
        {
            txtItemType.text = item.itemType.ToString();
        }

        // ===== 3. CẬP NHẬT CHỈ SỐ =====
        // Lấy toàn bộ chỉ số từ ScriptableObject ghép thành 1 đoạn Text lớn
        if (txtStats != null)
        {
            string finalContent = "";

            if (!string.IsNullOrEmpty(item.description))
            {
                finalContent += $"<i>{item.description}</i>\n"; // In nghiêng mô tả
            }

            if (item.itemType != ItemType.SupportSkill)
            {
                if (!string.IsNullOrEmpty(finalContent))
                    finalContent += "\n"; // Cách ra 1 dòng cho đẹp

                if (item.healthBonus > 0) finalContent += $"+{item.healthBonus} Health\n";
                if (item.attackBonus > 0) finalContent += $"+{item.attackBonus} Attack\n";
                if (item.defenseBonus > 0) finalContent += $"+{item.defenseBonus} Defense\n";
                if (item.critRateBonus > 0) finalContent += $"+{item.critRateBonus}% Crit Rate\n";
                if (item.critDamageBonus > 0) finalContent += $"+{item.critDamageBonus}% Crit Damage\n";
                if (item.speedBonus > 0) finalContent += $"+{item.speedBonus} Speed\n";
            }

            txtStats.text = finalContent;
        }

        // ===== 4. CẬP NHẬT GIÁ TIỀN =====
        if (txtItemPrice != null)
        {
            // Nếu có giá custom (từ Cửa hàng) thì lấy giá đó, không thì lấy giá sàn
            int displayPrice = customPrice >= 0 ? customPrice : GetBasePrice(item.rarity);
            txtItemPrice.text = displayPrice.ToString();
            txtItemPrice.gameObject.SetActive(true);
        }

        // ===== 5. BẬT GIAO DIỆN =====
        tooltipPanel.SetActive(true);

        // THUẬT TOÁN ÉP CHÍN UI KÍCH THƯỚC TOOLTIP & CĂN LỀ MÉP MÀN HÌNH
        if (rectTransform != null)
        {
            // Bắt buộc Unity phải vẽ lại cái bảng ngay lập tức để lấy được Width/Height thực tế (chưa vẽ xong thì thông số bằng 0)
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();

            if (parentRect != null)
            {
                Vector2 localPoint;

                // Quy đổi tọa độ chuột (Pixel trên màn hình) sang hệ tọa độ của Canvas
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    Input.mousePosition,
                    uiCamera,
                    out localPoint
                );

                Vector2 tooltipSize = rectTransform.rect.size; // Rộng x Dài của cái bảng
                Vector2 canvasSize = parentRect.rect.size;     // Kích thước của toàn màn hình

                // Mặc định Tooltip sẽ lệch so với đầu chuột xuống dưới 15px và qua phải 15px để tránh che mất con trỏ
                Vector2 offset = new Vector2(15f, -15f);

                // --- KIỂM TRA CHẠM MÉP BÊN PHẢI ---
                // Nếu Tọa độ X + Chiều rộng bảng > Cạnh phải màn hình -> Nhảy sang bên trái con chuột
                if (localPoint.x + tooltipSize.x > canvasSize.x * 0.5f)
                {
                    offset.x = -tooltipSize.x - 15f;
                }

                // --- KIỂM TRA CHẠM MÉP BÊN TRÁI ---
                if (localPoint.x - tooltipSize.x < -canvasSize.x * 0.5f)
                {
                    offset.x = 15f;
                }

                // --- KIỂM TRA CHẠM MÉP DƯỚI ---
                // Nếu bị tràn xuống gầm màn hình -> Hất nó lên trên con trỏ chuột
                if (localPoint.y - tooltipSize.y < -canvasSize.y * 0.5f)
                {
                    offset.y = tooltipSize.y + 15f;
                }

                // --- KIỂM TRA CHẠM MÉP TRÊN ---
                if (localPoint.y > canvasSize.y * 0.5f - 50f)
                {
                    offset.y = -15f;
                }

                // Gán vị trí cuối cùng
                rectTransform.anchoredPosition = localPoint + offset;
            }
        }

        // Delay đúng 1 khung hình (1 frame) rồi mới cho phép click để tắt (tránh double-trigger)
        StopAllCoroutines();
        StartCoroutine(EnableCloseNextFrame());
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        currentItem = null;
        canCloseTooltip = false;
    }
    #endregion

    #region PRIVATE METHODS
    /// <summary>
    /// Trả về màu sắc chuẩn cho Tên vật phẩm dựa vào mức độ Hiếm.
    /// </summary>
    private Color GetRarityColor(int rarityIndex)
    {
        switch (rarityIndex)
        {
            case 0: return Color.white;                 // Common: Trắng
            case 1: return new Color(0f, 0.5f, 1f);     // Rare: Xanh dương
            case 2: return new Color(0.6f, 0.2f, 0.8f); // Epic: Tím
            case 3: return new Color(1f, 0.6f, 0f);     // Legendary: Vàng Cam
            default: return Color.white;
        }
    }

    /// <summary>
    /// Tính giá Gốc (Base Price) mặc định. Nếu Shop muốn bán đắt hơn, Shop phải truyền customPrice vào.
    /// </summary>
    private int GetBasePrice(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 25;
            case ItemRarity.Rare: return 50;
            case ItemRarity.Epic: return 100;
            case ItemRarity.Legendary: return 150;
            default: return 25;
        }
    }
    #endregion

    #region COROUTINES
    private IEnumerator EnableCloseNextFrame()
    {
        canCloseTooltip = false;
        yield return null; // Đợi 1 frame
        canCloseTooltip = true;
    }
    #endregion
}