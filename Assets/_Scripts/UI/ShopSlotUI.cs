using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopSlotUI : MonoBehaviour
{
    public Image itemIcon;
    public TextMeshProUGUI txtPrice;
    public Button slotButton;

    private ItemSO currentItem;
    private int itemPrice;
    private ShopManager shopManager;

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;

    private void Awake()
    {
        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    public void SetupSlot(ItemSO item, int price, ShopManager manager)
    {
        currentItem = item;
        itemPrice = price;
        shopManager = manager;

        if (itemIcon != null && item != null)
        {
            itemIcon.sprite = item.icon;
        }

        if (txtPrice != null)
        {
            txtPrice.text = price.ToString();
        }
    }

    private void OnSlotClicked()
    {
        if (currentItem == null || shopManager == null) return;

        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            // DOUBLE CLICK: Mua luôn
            shopManager.SelectShopItem(currentItem, itemPrice, gameObject);
            shopManager.BuySelectedItem();
        }
        else
        {
            // SINGLE CLICK: Xem thông tin và đổi nút thành Buy
            shopManager.SelectShopItem(currentItem, itemPrice, gameObject);
        }
    }
}
