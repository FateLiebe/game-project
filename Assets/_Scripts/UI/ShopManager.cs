using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject playerStatsPanel;
    public GameObject shopPanel;
    public GameObject uiInventoryMenu;

    [Header("Shop Elements")]
    public Transform shopGridArea;
    public TextMeshProUGUI txtCoinShop;
    public TextMeshProUGUI txtGoldInventory;
    public Button btnSellBuy; // Nút này sẽ thay đổi Text giữa "Sell" và "Buy"
    public TextMeshProUGUI txtBtnSellBuy;

    [Header("Prefabs & Data")]
    public GameObject shopSlotPrefab; // Tạo prefab chứa ItemSlotUI và Text giá tiền
    public ItemDatabaseSO itemDatabase;
    
    [HideInInspector] public bool isShopOpen = false;
    private ItemSO selectedShopItem = null;
    private int selectedShopItemPrice = 0;
    private GameObject selectedShopSlotObj = null;
    private bool hasGeneratedShopItems = false;

    private List<GameObject> shopSlots = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (btnSellBuy != null)
        {
            btnSellBuy.onClick.AddListener(OnSellBuyClicked);
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        hasGeneratedShopItems = false;
    }

    public void OpenShop()
    {
        isShopOpen = true;

        if (uiInventoryMenu != null) uiInventoryMenu.SetActive(true);
        if (playerStatsPanel != null) playerStatsPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(true);

        if (txtGoldInventory != null) txtGoldInventory.gameObject.SetActive(false);
        
        btnSellBuy.gameObject.SetActive(true);
        txtBtnSellBuy.text = "Sell"; // Mặc định hiển thị Sell khi mở

        AudioManager.Instance?.PlayInventoryOpen();

        UpdateCoinDisplay();
        
        if (!hasGeneratedShopItems)
        {
            GenerateShopItems();
            hasGeneratedShopItems = true;
        }
    }

    public void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null) shopPanel.SetActive(false);
        if (playerStatsPanel != null) playerStatsPanel.SetActive(true);
        
        if (txtGoldInventory != null) txtGoldInventory.gameObject.SetActive(true);
        btnSellBuy.gameObject.SetActive(false);

        if (uiInventoryMenu != null) uiInventoryMenu.SetActive(false);
        
        AudioManager.Instance?.PlayInventoryClose();
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }

    public void UpdateCoinDisplay()
    {
        if (PlayerController.Instance != null)
        {
            if (txtCoinShop != null) txtCoinShop.text = PlayerController.Instance.coins.ToString();
            if (txtGoldInventory != null) txtGoldInventory.text = PlayerController.Instance.coins.ToString();
        }
    }

    private void GenerateShopItems()
    {
        // Xóa các item cũ
        foreach (var slot in shopSlots) Destroy(slot);
        shopSlots.Clear();

        if (itemDatabase == null || itemDatabase.allItemsInGame.Count == 0) return;

        // Sinh 15 item ngẫu nhiên
        for (int i = 0; i < 15; i++)
        {
            ItemSO randomItem = itemDatabase.allItemsInGame[Random.Range(0, itemDatabase.allItemsInGame.Count)];
            GameObject slotObj = Instantiate(shopSlotPrefab, shopGridArea);
            shopSlots.Add(slotObj);

            int basePrice = GetBasePrice(randomItem);
            int shopPrice = Mathf.RoundToInt(basePrice * 5 * Random.Range(0.9f, 1.1f));

            ShopSlotUI shopSlot = slotObj.GetComponent<ShopSlotUI>();
            if (shopSlot != null)
            {
                shopSlot.SetupSlot(randomItem, shopPrice, this);
            }
        }
    }

    private int GetBasePrice(ItemSO item)
    {
        switch (item.rarity)
        {
            case ItemRarity.Common: return 25;
            case ItemRarity.Rare: return 50;
            case ItemRarity.Epic: return 100;
            case ItemRarity.Legendary: return 150;
            default: return 25;
        }
    }

    // Được gọi khi click vào item trong Shop
    public void SelectShopItem(ItemSO item, int price, GameObject slotObj)
    {
        selectedShopItem = item;
        selectedShopItemPrice = price;
        selectedShopSlotObj = slotObj;
        
        txtBtnSellBuy.text = "Buy";
        InventoryManager.Instance?.ShowTooltip(item, price);
    }

    // Được gọi khi click vào item trong Inventory (bên phải)
    public void SelectInventoryItemToSell()
    {
        txtBtnSellBuy.text = "Sell";
    }

    public void BuySelectedItem()
    {
        if (selectedShopItem == null) return;

        if (PlayerController.Instance.coins >= selectedShopItemPrice)
        {
            PlayerController.Instance.coins -= selectedShopItemPrice;
            InventoryManager.Instance.AddItem(selectedShopItem);
            UpdateCoinDisplay();
            AudioManager.Instance?.PlayEquip(); // Hoặc âm thanh mua bán

            // Mua xong thì xóa khỏi shop (Ẩn đi để không làm xô lệch các ô khác trong Grid)
            if (selectedShopSlotObj != null)
            {
                ShopSlotUI slotUI = selectedShopSlotObj.GetComponent<ShopSlotUI>();
                if (slotUI != null)
                {
                    if (slotUI.itemIcon != null) slotUI.itemIcon.enabled = false; // [FIX] Tắt hiển thị ảnh thay vì tắt cả Object
                    if (slotUI.txtPrice != null) slotUI.txtPrice.transform.parent.gameObject.SetActive(false);
                    if (slotUI.slotButton != null) slotUI.slotButton.interactable = false;
                }
                shopSlots.Remove(selectedShopSlotObj); // Xóa khỏi danh sách để không duyệt tới
            }
            selectedShopItem = null;
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
        else
        {
            // Báo lỗi thiếu tiền trực tiếp lên nút bấm (vì DamagePopup bị UI che khuất)
            if (notEnoughCoinCoroutine != null) StopCoroutine(notEnoughCoinCoroutine);
            notEnoughCoinCoroutine = StartCoroutine(NotEnoughCoinRoutine());
            AudioManager.Instance?.PlayUIClick(); // Có thể thay bằng âm thanh báo lỗi nếu có
        }
    }

    private Coroutine notEnoughCoinCoroutine;
    private System.Collections.IEnumerator NotEnoughCoinRoutine()
    {
        txtBtnSellBuy.text = "<color=red>Not enough!</color>";
        
        // --- HIỆU ỨNG TEXT NỔI LÊN GIỮA MÀN HÌNH ---
        GameObject warningObj = new GameObject("WarningText");
        warningObj.transform.SetParent(shopPanel.transform, false);
        
        TMPro.TextMeshProUGUI tmp = warningObj.AddComponent<TMPro.TextMeshProUGUI>(); // Tự động có RectTransform
        
        RectTransform rt = warningObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 0); // Giữa màn hình
        rt.sizeDelta = new Vector2(800, 100);
        tmp.text = "NOT ENOUGH COIN!";
        tmp.color = Color.red;
        tmp.fontSize = 60;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        
        float timer = 0f;
        float duration = 2f; // [FIX] Tăng thời gian hiển thị từ 1s lên 2s
        Vector2 startPos = rt.anchoredPosition;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            // [FIX] Bay lên từ từ hơn (100px trong 2s thay vì 150px trong 1s)
            rt.anchoredPosition = startPos + new Vector2(0, 100f * (timer / duration)); 
            
            // [FIX] Cho chữ giữ nguyên màu trong 1s đầu, 1s sau mới bắt đầu mờ dần
            float alpha = timer < 1f ? 1f : 1f - ((timer - 1f) / 1f);
            tmp.color = new Color(1, 0, 0, alpha); 
            
            yield return null;
        }
        
        Destroy(warningObj);
        
        // Reset nút
        if (txtBtnSellBuy.text.Contains("Not enough")) 
        {
            txtBtnSellBuy.text = "Buy";
        }
    }

    public void SellItem(ItemSO item)
    {
        if (item == null) return;

        int price = GetBasePrice(item);
        PlayerController.Instance.coins += price;
        InventoryManager.Instance.RemoveItem(item, 1);
        UpdateCoinDisplay();
        AudioManager.Instance?.PlayUnequip(); // Hoặc âm thanh mua bán
        
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }

    private void OnSellBuyClicked()
    {
        AudioManager.Instance?.PlayUIClick();
        
        if (txtBtnSellBuy.text == "Buy")
        {
            BuySelectedItem();
        }
        else if (txtBtnSellBuy.text == "Sell")
        {
            // Bán item đang được chọn bên InventoryManager
            ItemSO invSelectedItem = InventoryManager.Instance.GetSelectedItem();
            if (invSelectedItem != null)
            {
                SellItem(invSelectedItem);
            }
        }
    }
}
