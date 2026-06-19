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
    [SerializeField] private TextMeshProUGUI txtItemPrice; // [FIX] Sử dụng Txt_ItemPrice có sẵn

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

            // [FIX] Tự động tìm TXT_ItemPrice nếu bạn quên kéo thả trên Inspector
            if (txtItemPrice == null)
            {
                Transform priceTrans = tooltipPanel.transform.Find("TXT_ItemPrice");
                if (priceTrans != null) txtItemPrice = priceTrans.GetComponent<TextMeshProUGUI>();
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

    public void ShowTooltip(ItemSO item, int customPrice = -1)
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
                    finalContent += $"+{item.healthBonus} Health\n";

                if (item.attackBonus > 0)
                    finalContent += $"+{item.attackBonus} Attack\n";

                if (item.defenseBonus > 0)
                    finalContent += $"+{item.defenseBonus} Defense\n";

                if (item.critRateBonus > 0)
                    finalContent += $"+{item.critRateBonus}% Crit Rate\n";

                if (item.critDamageBonus > 0)
                    finalContent += $"+{item.critDamageBonus}% Crit Damage\n";

                if (item.speedBonus > 0)
                    finalContent += $"+{item.speedBonus} Speed\n";
            }

            txtStats.text = finalContent;
        }

        // ===== ITEM PRICE =====
        if (txtItemPrice != null)
        {
            int displayPrice = customPrice >= 0 ? customPrice : GetBasePrice(item.rarity);
            txtItemPrice.text = displayPrice.ToString();
            txtItemPrice.gameObject.SetActive(true); // [FIX] Bật chính txtItemPrice
        }

        // ===== BAT TOOLTIP =====
        tooltipPanel.SetActive(true);

        if (rectTransform != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();

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

                // Kich thuoc tooltip sau khi rebuild
                Vector2 tooltipSize = rectTransform.rect.size;

                // Kich thuoc canvas
                Vector2 canvasSize = parentRect.rect.size;

                // Mac dinh hien ben phai duoi chuot
                Vector2 offset = new Vector2(15f, -15f);

                // ===== KIEM TRA MEP PHAI =====
                if (localPoint.x + tooltipSize.x > canvasSize.x * 0.5f)
                {
                    offset.x = -tooltipSize.x - 15f;
                }

                // ===== KIEM TRA MEP TRAI =====
                if (localPoint.x - tooltipSize.x < -canvasSize.x * 0.5f)
                {
                    offset.x = 15f;
                }

                // ===== KIEM TRA MEP DUOI =====
                if (localPoint.y - tooltipSize.y < -canvasSize.y * 0.5f)
                {
                    offset.y = tooltipSize.y + 15f;
                }

                // ===== KIEM TRA MEP TREN =====
                if (localPoint.y > canvasSize.y * 0.5f - 50f)
                {
                    offset.y = -15f;
                }

                rectTransform.anchoredPosition = localPoint + offset;
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
}