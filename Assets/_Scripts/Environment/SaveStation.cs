using UnityEngine;

/// <summary>
/// Trạm Lưu Game (Checkpoint).
/// Khi Player chạm vào, tự động gom dữ liệu hiện tại (Máu, Túi đồ, Tọa độ) gửi cho SaveDataManager để ghi đè vào file save.
/// </summary>
public class SaveStation : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerController player = collision.GetComponent<PlayerController>();
            InventoryManager inv = FindAnyObjectByType<InventoryManager>();

            if (player != null && inv != null && SaveDataManager.Instance != null)
            {
                SaveDataManager.Instance.SaveAtCheckpoint(player, inv, transform);
                Debug.Log("<color=green>ĐÃ ĐẾN TRẠM CHECKPOINT! AUTO-SAVE THÀNH CÔNG!</color>");
            }
        }
    }
}
