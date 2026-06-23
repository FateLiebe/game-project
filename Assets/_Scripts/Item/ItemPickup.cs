using UnityEngine;
using System.Collections;

/// <summary>
/// Cấu hình và quản lý vật phẩm rơi trên mặt đất.
/// Xử lý việc chống nhặt ngay lập tức khi vừa văng ra, và tự động mờ dần rồi biến mất để dọn rác.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public ItemSO itemData;
    private SpriteRenderer sr;
    private Collider2D col;
    private bool canPickUp = true;

    [Tooltip("Thời gian trước khi item tự biến mất (giây). 0 = không bao giờ biến mất")]
    public float despawnTime = 45f;
    private const float FADE_DURATION = 5f; // Mờ dần 5s trước khi xóa
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (itemData != null && sr != null) sr.sprite = itemData.icon;
        if (sr != null) sr.color = Color.white; // Reset màu nếu tái sử dụng
        if (despawnTime > 0f) StartCoroutine(DespawnRoutine());
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
            ReturnOrDestroy();
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Gắn dữ liệu vật phẩm (ItemSO) vào model rớt trên sàn.
    /// Hàm này thường được gọi bởi InventoryManager hoặc DropManager khi vứt/rớt đồ.
    /// </summary>
    public void Setup(ItemSO item, bool lockPickup = false)
    {
        itemData = item;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && itemData != null) sr.sprite = itemData.icon;

        StartCoroutine(SetupCoroutine(lockPickup)); // Bắt đầu Coroutine để tạm thời vô hiệu hóa nhặt đồ
    }
    #endregion

    #region COROUTINES
    /// <summary>
    /// Vô hiệu hóa Collider 1 giây khi vật phẩm vừa sinh ra. 
    /// Kích hoạt trạng thái khóa nhặt đồ trong thời gian ngắn để tạo cảm giác vật phẩm văng ra chạm đất rồi mới nhặt được.
    /// </summary>
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

    /// <summary>
    /// Tự động đếm ngược thời gian (despawnTime). Khi gần hết giờ (còn 5 giây), vật phẩm sẽ nhấp nháy/mờ dần rồi bị hủy để chống Memory Leak.
    /// </summary>
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
        ReturnOrDestroy();
    }
    #endregion

    #region PRIVATE METHODS
    /// <summary>Trả về pool nếu được tạo qua ObjectPoolManager, ngược lại thì Destroy.</summary>
    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
    #endregion
}