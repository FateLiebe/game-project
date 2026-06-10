using UnityEngine;
using System.Collections; // BẮT BUỘC phải có dòng này để chạy thời gian (Coroutine)

public class ItemPickup : MonoBehaviour
{
    public ItemSO itemData;
    private SpriteRenderer sr;
    private bool canPickUp = true; // Mặc định cho phép nhặt (dành cho đồ rớt từ thùng)

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (itemData != null && sr != null) sr.sprite = itemData.icon;
    }

    // Hàm này được gọi bởi InventoryManager khi bạn bấm nút "Vứt bỏ"
    public void Setup(ItemSO item)
    {
        itemData = item;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && itemData != null) sr.sprite = itemData.icon;
        
        // Vừa vứt ra khỏi túi -> Khóa không cho nhặt ngay lập tức
        StartCoroutine(PickupCooldown());
    }

    private IEnumerator PickupCooldown()
    {
        canPickUp = false; // Tắt cảm biến nhặt
        
        // Đợi 1.5 giây để cục đồ có thời gian nảy lên và rơi hẳn xuống đất
        yield return new WaitForSeconds(1.5f); 
        
        canPickUp = true; // Bật lại cảm biến nhặt
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!canPickUp) return;
        if (!other.CompareTag("Player")) return;
        if (itemData == null) return;
        
        // Guard null
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryManager.Instance NULL khi nhặt đồ!");
            return;
        }
        
        if (InventoryManager.Instance.AddItem(itemData))
            Destroy(gameObject);
    }
}