using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Quản lý toàn bộ hệ thống Cửa hàng (Shop) bao gồm sinh vật phẩm ngẫu nhiên, mua/bán và giao diện UI.
/// Hoạt động theo mô hình Singleton để dễ dàng truy cập từ các script khác.
/// </summary>
public class ShopManager : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static ShopManager Instance { get; private set; }

    [Header("UI Panels")]
    [Tooltip("Panel hiển thị chỉ số nhân vật (sẽ bị tắt khi mở Shop)")]
    public GameObject playerStatsPanel;
    [Tooltip("Panel chính chứa các mặt hàng của Shop")]
    public GameObject shopPanel;
    [Tooltip("Panel Menu Túi đồ (dùng chung cho cả mở Inventory và mở Shop)")]
    public GameObject uiInventoryMenu;

    [Header("Shop Elements")]
    [Tooltip("Khu vực dạng lưới (Grid) để chứa các ô vật phẩm được sinh ra")]
    public Transform shopGridArea;
    [Tooltip("Đoạn Text hiển thị số lượng Coin hiện có (nằm trên góc bảng Shop)")]
    public TextMeshProUGUI txtCoinShop;
    [Tooltip("Đoạn Text hiển thị số lượng Coin hiện có (nằm bên bảng Inventory)")]
    public TextMeshProUGUI txtGoldInventory;
    [Tooltip("Nút bấm duy nhất dùng cho cả thao tác Mua và Bán")]
    public Button btnSellBuy;
    [Tooltip("Chữ trên nút (sẽ tự động đổi thành 'Buy' hoặc 'Sell' tùy vào việc click item ở bên nào)")]
    public TextMeshProUGUI txtBtnSellBuy;

    [Header("Prefabs & Data")]
    [Tooltip("Prefab của từng ô vật phẩm (ShopSlot)")]
    public GameObject shopSlotPrefab; 
    [Tooltip("Kho dữ liệu toàn bộ vật phẩm có trong game để bốc ngẫu nhiên")]
    public ItemDatabaseSO itemDatabase;
    
    [HideInInspector] public bool isShopOpen = false; // Trạng thái đóng/mở của Shop

    // --- CÁC BIẾN LƯU TRỮ TẠM THỜI (CACHE) ---
    private ItemSO selectedShopItem = null;         // Món hàng đang được người chơi click chọn
    private int selectedShopItemPrice = 0;          // Giá tiền của món hàng đó
    private GameObject selectedShopSlotObj = null;  // Object ô chứa món hàng đó (để lát nữa mua xong thì làm mờ đi)
    
    // Cờ đánh dấu xem Shop đã tạo đồ ngẫu nhiên trong Scene này chưa (chỉ tạo 1 lần mỗi khi vào Map)
    private bool hasGeneratedShopItems = false;

    // Danh sách lưu lại tất cả các ô vật phẩm đã tạo ra để dễ dàng xóa sạch khi cần
    private List<GameObject> shopSlots = new List<GameObject>();
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // Lắng nghe sự kiện click nút Buy/Sell
        if (btnSellBuy != null)
        {
            btnSellBuy.onClick.AddListener(OnSellBuyClicked);
        }
        
        // Đăng ký sự kiện: Mỗi khi chuyển sang một Map mới (Scene mới) thì gọi hàm OnSceneLoaded
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Hủy đăng ký sự kiện để tránh lỗi bộ nhớ khi Object này bị hủy
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Được gọi tự động mỗi khi người chơi đi qua cổng sang một Map mới.
    /// Giúp reset lại cờ, để khi mở Shop ở Map mới thì hàng hóa sẽ được random mới lại từ đầu.
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        hasGeneratedShopItems = false;
    }
    #endregion

    #region UI CONTROLS & UPDATES
    /// <summary>
    /// Mở giao diện Cửa hàng, sắp xếp lại các Panel và tạo hàng hóa nếu chưa tạo.
    /// </summary>
    public void OpenShop()
    {
        isShopOpen = true;

        // Bật túi đồ, tắt bảng chỉ số, bật bảng Shop thay thế vào chỗ đó
        if (uiInventoryMenu != null) uiInventoryMenu.SetActive(true);
        if (playerStatsPanel != null) playerStatsPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(true);

        // Ẩn hiển thị vàng thừa thãi để giao diện gọn gàng hơn
        if (txtGoldInventory != null) txtGoldInventory.gameObject.SetActive(false);
        
        // Cấu hình lại nút bấm mặc định là Bán (Sell)
        btnSellBuy.gameObject.SetActive(true);
        txtBtnSellBuy.text = "Sell"; 

        AudioManager.Instance?.PlayInventoryOpen(); // Phát âm thanh mở túi

        UpdateCoinDisplay(); // Cập nhật lại số tiền hiển thị
        
        // Chỉ bốc ngẫu nhiên vật phẩm 1 lần duy nhất trong suốt thời gian ở Map này
        if (!hasGeneratedShopItems)
        {
            GenerateShopItems();
            hasGeneratedShopItems = true;
        }
    }

    /// <summary>
    /// Đóng cửa hàng và trả giao diện UI về trạng thái Túi Đồ bình thường.
    /// </summary>
    public void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null) shopPanel.SetActive(false);
        if (playerStatsPanel != null) playerStatsPanel.SetActive(true); // Bật lại bảng chỉ số
        
        if (txtGoldInventory != null) txtGoldInventory.gameObject.SetActive(true);
        btnSellBuy.gameObject.SetActive(false); // Ẩn nút Buy/Sell đi

        if (uiInventoryMenu != null) uiInventoryMenu.SetActive(false);
        
        AudioManager.Instance?.PlayInventoryClose();
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }

    /// <summary>
    /// Cập nhật hiển thị số Coin hiện có trên các Text UI.
    /// </summary>
    public void UpdateCoinDisplay()
    {
        if (PlayerController.Instance != null)
        {
            if (txtCoinShop != null) txtCoinShop.text = PlayerController.Instance.coins.ToString();
            if (txtGoldInventory != null) txtGoldInventory.text = PlayerController.Instance.coins.ToString();
        }
    }
    #endregion

    #region SHOP LOGIC
    /// <summary>
    /// Thuật toán sinh ngẫu nhiên 15 mặt hàng để bán trong Shop.
    /// Tính toán giá đội lên (thay đổi giá trị ngẫu nhiên chút xíu) cho từng mặt hàng.
    /// </summary>
    private void GenerateShopItems()
    {
        // 1. Quét sạch hàng hóa cũ (nếu có)
        foreach (var slot in shopSlots) Destroy(slot);
        shopSlots.Clear();

        if (itemDatabase == null || itemDatabase.allItemsInGame.Count == 0) return;

        // 2. Sinh 15 ô item mới
        for (int i = 0; i < 15; i++)
        {
            // Bốc ngẫu nhiên 1 loại vật phẩm từ Data
            ItemSO randomItem = itemDatabase.allItemsInGame[Random.Range(0, itemDatabase.allItemsInGame.Count)];
            GameObject slotObj = Instantiate(shopSlotPrefab, shopGridArea);
            shopSlots.Add(slotObj);

            // Tính toán giá bán của Cửa hàng: BasePrice x 5 lần x hệ số dao động (0.9 đến 1.1)
            int basePrice = GetBasePrice(randomItem);
            int shopPrice = Mathf.RoundToInt(basePrice * 5 * Random.Range(0.9f, 1.1f));

            // Đổ dữ liệu vào giao diện của ô đó
            ShopSlotUI shopSlot = slotObj.GetComponent<ShopSlotUI>();
            if (shopSlot != null)
            {
                shopSlot.SetupSlot(randomItem, shopPrice, this);
            }
        }
    }

    /// <summary>
    /// Tính toán Giá Gốc (Base Price) của một vật phẩm dựa vào Độ hiếm (Rarity) của nó.
    /// </summary>
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

    /// <summary>
    /// Gọi bởi ShopSlotUI mỗi khi người chơi click vào 1 món hàng bên bảng Shop.
    /// Nó sẽ nhớ mặt hàng này, đổi nút thành Buy và gọi Tooltip hiển thị.
    /// </summary>
    public void SelectShopItem(ItemSO item, int price, GameObject slotObj)
    {
        selectedShopItem = item;
        selectedShopItemPrice = price;
        selectedShopSlotObj = slotObj;
        
        txtBtnSellBuy.text = "Buy"; // Chuẩn bị cho việc mua
        InventoryManager.Instance?.ShowTooltip(item, price); // Hiển thị giá đang bán
    }

    /// <summary>
    /// Gọi mỗi khi người chơi click vào 1 món hàng bên bảng Túi đồ (Inventory).
    /// </summary>
    public void SelectInventoryItemToSell()
    {
        txtBtnSellBuy.text = "Sell"; // Đổi chức năng nút thành Bán
    }
    #endregion

    #region BUY & SELL LOGIC
    /// <summary>
    /// Xử lý logic Mua vật phẩm (Trừ tiền, thêm đồ, làm trống ô trên kệ).
    /// </summary>
    public void BuySelectedItem()
    {
        if (selectedShopItem == null) return;

        // Kiểm tra xem túi có đủ tiền không
        if (PlayerController.Instance.coins >= selectedShopItemPrice)
        {
            PlayerController.Instance.coins -= selectedShopItemPrice; // Trừ tiền
            InventoryManager.Instance.AddItem(selectedShopItem);      // Thêm vào túi đồ
            UpdateCoinDisplay();
            AudioManager.Instance?.PlayEquip(); // Tiếng ching ching mua bán
            
            // Xử lý giao diện: Làm cho ô hàng này biến thành ô trống thay vì Destroy gây xô lệch Grid
            if (selectedShopSlotObj != null)
            {
                ShopSlotUI slotUI = selectedShopSlotObj.GetComponent<ShopSlotUI>();
                if (slotUI != null)
                {
                    if (slotUI.itemIcon != null) slotUI.itemIcon.enabled = false; // Tắt hình món hàng
                    if (slotUI.txtPrice != null) slotUI.txtPrice.transform.parent.gameObject.SetActive(false); // Tắt nhãn giá
                    if (slotUI.slotButton != null) slotUI.slotButton.interactable = false; // Không cho click nữa
                }
                shopSlots.Remove(selectedShopSlotObj); // Bỏ qua nó trong danh sách
            }
            
            selectedShopItem = null;
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip(); // Ẩn Tooltip đi
        }
        else
        {
            // THIẾU TIỀN: Bắn hiệu ứng cảnh báo
            if (notEnoughCoinCoroutine != null) StopCoroutine(notEnoughCoinCoroutine);
            notEnoughCoinCoroutine = StartCoroutine(NotEnoughCoinRoutine());
            AudioManager.Instance?.PlayUIClick(); // Âm thanh lỗi báo hiệu thiếu tiền
        }
    }

    /// <summary>
    /// Xử lý logic Bán vật phẩm (Cộng tiền, xóa đồ).
    /// </summary>
    public void SellItem(ItemSO item)
    {
        if (item == null) return;

        int price = GetBasePrice(item); // Bán thì chỉ nhận được Base Price (Giá gốc) thôi
        PlayerController.Instance.coins += price;
        InventoryManager.Instance.RemoveItem(item, 1);
        UpdateCoinDisplay();
        
        AudioManager.Instance?.PlayUnequip(); // Tiếng mua bán
        
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }

    /// <summary>
    /// Xử lý đầu vào khi người chơi click vào nút trung tâm (Nút Sell/Buy).
    /// </summary>
    private void OnSellBuyClicked()
    {
        AudioManager.Instance?.PlayUIClick();
        
        // Phân luồng dựa trên chữ đang hiển thị trên nút
        if (txtBtnSellBuy.text == "Buy")
        {
            BuySelectedItem();
        }
        else if (txtBtnSellBuy.text == "Sell")
        {
            // Lấy món hàng đang được Focus bên khu vực Túi Đồ
            ItemSO invSelectedItem = InventoryManager.Instance.GetSelectedItem();
            if (invSelectedItem != null)
            {
                SellItem(invSelectedItem);
            }
        }
    }
    #endregion

    #region COROUTINES
    // Biến giữ Coroutine để nếu click liên tục thì ngắt cái cũ, chạy lại cái mới
    private Coroutine notEnoughCoinCoroutine;

    /// <summary>
    /// Coroutine tạo một luồng chữ "NOT ENOUGH COIN!" bay lên giữa màn hình cảnh báo.
    /// Chữ sẽ mờ dần sau 1 giây đầu và biến mất hoàn toàn ở giây thứ 2.
    /// </summary>
    private System.Collections.IEnumerator NotEnoughCoinRoutine()
    {
        // Đổi ngay chữ trên nút thành cảnh báo đỏ
        txtBtnSellBuy.text = "<color=red>Not enough!</color>";
        
        // --- HIỆU ỨNG TEXT NỔI LÊN GIỮA MÀN HÌNH ---
        // Sinh ra một GameObject Text hoàn toàn bằng code (Dynamic UI)
        GameObject warningObj = new GameObject("WarningText");
        warningObj.transform.SetParent(shopPanel.transform, false);
        
        TMPro.TextMeshProUGUI tmp = warningObj.AddComponent<TMPro.TextMeshProUGUI>(); 
        RectTransform rt = warningObj.GetComponent<RectTransform>();
        
        // Canh giữa màn hình
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 0); 
        rt.sizeDelta = new Vector2(800, 100);
        
        // Trang trí cho chữ
        tmp.text = "NOT ENOUGH COIN!";
        tmp.color = Color.red;
        tmp.fontSize = 60;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        
        float timer = 0f;
        float duration = 2f; 
        Vector2 startPos = rt.anchoredPosition;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            
            // Animation trôi dần lên trên (tổng cộng trôi 100 pixel)
            rt.anchoredPosition = startPos + new Vector2(0, 100f * (timer / duration)); 
            
            // Tính toán độ mờ: Giữ nguyên màu trong 1 giây đầu (alpha = 1), sau đó mờ dần về 0
            float alpha = timer < 1f ? 1f : 1f - ((timer - 1f) / 1f);
            tmp.color = new Color(1, 0, 0, alpha); 
            
            yield return null;
        }
        
        Destroy(warningObj); // Dọn dẹp Text rác
        
        // Trả lại chữ Buy cho nút bấm
        if (txtBtnSellBuy.text.Contains("Not enough")) 
        {
            txtBtnSellBuy.text = "Buy";
        }
    }
    #endregion
}
