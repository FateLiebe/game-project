using UnityEngine;
using System.Collections; // BẮT BUỘC phải có dòng này để chạy thời gian (Coroutine)

public class ItemPickup : MonoBehaviour
{
    public ItemSO itemData;
    private SpriteRenderer sr;
    private Collider2D col;
    private bool canPickUp = true;

    [Tooltip("Thời gian trước khi item tự biến mất (giây). 0 = không bao giờ biến mất")]
    public float despawnTime = 45f;
    private const float FADE_DURATION = 5f; // Mờ dần 5s trước khi xóa

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void Start()
    {
        if (itemData != null && sr != null) sr.sprite = itemData.icon;
        if (despawnTime > 0f) StartCoroutine(DespawnRoutine());
    }

    // Hàm này được gọi bởi InventoryManager khi bạn bấm nút "Vứt bỏ"
    public void Setup(ItemSO item, bool lockPickup = false)
    {
        itemData = item;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && itemData != null) sr.sprite = itemData.icon;

        StartCoroutine(SetupCoroutine(lockPickup)); // Bắt đầu Coroutine để tạm thời vô hiệu hóa nhặt đồ
    }

    private IEnumerator SetupCoroutine(bool lockPickup)
    {
        canPickUp = false; // Tắt cảm biến nhặt ngay khi setup
        if (col != null) col.enabled = false; // Vô hiệu hóa collider để tránh nhặt ngay lập tức

        // Đợi 1 giây để đảm bảo đồ đã nảy lên và rơi xuống đất
        yield return new WaitForSeconds(1f); 

        canPickUp = true; // Bật lại cảm biến nhặt sau khi setup xong
        if (col != null) col.enabled = true; // Kích hoạt lại collider để cho phép nhặt đồ

        if (lockPickup)
        {
            StartCoroutine(PickupCooldown());
        }
        else
        {
            canPickUp = true;
        }
    }

    private IEnumerator PickupCooldown()
    {
        canPickUp = false; // Tắt cảm biến nhặt ngay khi setup
        if (col != null) col.enabled = false; // Vô hiệu hóa collider để tránh nhặt ngay lập tức

        // Đợi 1.5 giây để đảm bảo đồ đã nảy lên và rơi xuống đất
        yield return new WaitForSeconds(1.5f); 

        canPickUp = true; // Bật lại cảm biến nhặt sau khi setup xong
        if (col != null) col.enabled = true; // Kích hoạt lại collider để cho phép nhặt đồ
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!canPickUp) return;
        if (!other.CompareTag("Player")) return;
        if (itemData == null) return;
        
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryManager.Instance NULL khi nhặt đồ!");
            return;
        }
        
        if (InventoryManager.Instance.AddItem(itemData))
            Destroy(gameObject);
    }

    private IEnumerator DespawnRoutine()
    {
        float waitTime = Mathf.Max(0f, despawnTime - FADE_DURATION);
        yield return new WaitForSeconds(waitTime);

        // Mờ dần
        float elapsed = 0f;
        Color c = sr != null ? sr.color : Color.white;
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            if (sr != null)
            {
                c.a = 1f - (elapsed / FADE_DURATION);
                sr.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}