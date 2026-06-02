using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI txtItemName;
    [SerializeField] private TextMeshProUGUI txtItemType;
    [SerializeField] private TextMeshProUGUI txtStats;

    private RectTransform rectTransform;
    private RectTransform parentRect;

    private ItemSO currentItem;

    private Color originalNameColor;
    private float baseBlinkSpeed = 2f;

    private Canvas parentCanvas;
    private Camera uiCamera;

    // Cho phep click tat tooltip sau 1 frame
    private bool canCloseTooltip;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (tooltipPanel != null)
        {
            rectTransform = tooltipPanel.GetComponent<RectTransform>();

            if (rectTransform != null)
            {
                rectTransform.pivot = new Vector2(0f, 1f);

                // Cache parentRect ngay tu dau
                parentRect = rectTransform.parent as RectTransform;
            }
        }

        // Tim canvas ke ca khi object dang tat
        parentCanvas = GetComponentInParent<Canvas>(true);

        if (parentCanvas != null &&
            parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        HideTooltip();
    }

    private void Update()
    {
        if (tooltipPanel == null || !tooltipPanel.activeSelf)
            return;

        // Click ngoai de tat tooltip
        if (canCloseTooltip && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform,
                Input.mousePosition,
                uiCamera))
            {
                HideTooltip();
                return;
            }
        }

        // Hieu ung nhap nhay ten item
        if (currentItem != null && txtItemName != null)
        {
            float rarityMultiplier = (int)currentItem.rarity + 1f;
            float currentSpeed = baseBlinkSpeed * rarityMultiplier;

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

    public void ShowTooltip(ItemSO item)
    {
        if (item == null || tooltipPanel == null)
            return;

        currentItem = item;

        // ===== ITEM NAME =====
        if (txtItemName != null)
        {
            txtItemName.text = item.itemName;

            originalNameColor = GetRarityColor((int)item.rarity);

            txtItemName.color = originalNameColor;
        }

        // ===== ITEM TYPE =====
        if (txtItemType != null)
        {
            txtItemType.text = item.itemType.ToString();
        }

        // ===== ITEM STATS =====
        if (txtStats != null)
        {
            string finalContent = "";

            if (!string.IsNullOrEmpty(item.description))
            {
                finalContent += $"<i>{item.description}</i>\n";
            }

            if (item.itemType != ItemType.SupportSkill)
            {
                if (!string.IsNullOrEmpty(finalContent))
                    finalContent += "\n";

                if (item.healthBonus > 0)
                    finalContent += $"+{item.healthBonus} Máu\n";

                if (item.attackBonus > 0)
                    finalContent += $"+{item.attackBonus} Tấn công\n";

                if (item.defenseBonus > 0)
                    finalContent += $"+{item.defenseBonus} Phòng thủ\n";

                if (item.critRateBonus > 0)
                    finalContent += $"+{item.critRateBonus}% Tỉ lệ bạo kích\n";

                if (item.critDamageBonus > 0)
                    finalContent += $"+{item.critDamageBonus}% ST bạo kích\n";

                if (item.speedBonus > 0)
                    finalContent += $"+{item.speedBonus} Tốc độ\n";
            }

            txtStats.text = finalContent;
        }

        // ===== BAT TOOLTIP =====
        tooltipPanel.SetActive(true);

        // Ep Unity rebuild UI ngay lap tuc
        Canvas.ForceUpdateCanvases();

        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            // Dat tooltip tai vi tri chuot
            if (parentRect != null)
            {
                Vector2 localPoint;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    Input.mousePosition,
                    uiCamera,
                    out localPoint
                );

                // [ĐÃ SỬA]: Nhích Tooltip sang phải 15 pixel và xuống dưới 15 pixel
                // Để nó hoàn toàn né con trỏ chuột, trả lại vùng không gian cho cú click thứ 2
                localPoint += new Vector2(15f, -15f);

                rectTransform.anchoredPosition = localPoint;
            }
        }

        // Delay dung 1 frame moi cho phep click tat
        StopAllCoroutines();
        StartCoroutine(EnableCloseNextFrame());
    }

    private IEnumerator EnableCloseNextFrame()
    {
        canCloseTooltip = false;

        yield return null;

        canCloseTooltip = true;
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        currentItem = null;

        canCloseTooltip = false;
    }

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
}