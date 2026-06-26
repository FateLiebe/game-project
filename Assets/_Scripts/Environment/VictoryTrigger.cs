using UnityEngine;

/// <summary>
/// Vùng kích hoạt Chiến thắng (Win Zone).
/// Hỗ trợ 2 chế độ:
///   1. Trigger Zone: Đặt tại cuối Map. Khi Player bước vào sẽ kích hoạt Victory.
///   2. Boss Mode: Gán trực tiếp vào GameObject Boss. Khi Boss bị tiêu diệt (isDead == true),
///      hệ thống sẽ tự động Lưu Game rồi hiển thị màn hình Victory.
/// </summary>
public class VictoryTrigger : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("--- CHẾ ĐỘ HOẠT ĐỘNG ---")]
    [Tooltip("Bật ON nếu script này được gắn vào Boss. Tắt OFF nếu đây là vùng Trigger ở cuối Map.")]
    public bool isBossMode = false;

    private BossController bossController;
    private bool victoryTriggered = false;
    #endregion

    #region UNITY LIFECYCLE
    private void Start()
    {
        // Nếu ở chế độ Boss, tự động tìm và lưu reference của BossController trên cùng GameObject
        if (isBossMode)
        {
            bossController = GetComponent<BossController>();
            if (bossController == null)
            {
                Debug.LogWarning("[VictoryTrigger] isBossMode = true nhưng không tìm thấy BossController trên cùng GameObject! Hãy kiểm tra lại.", this);
            }
        }
    }

    private void Update()
    {
        // Ở chế độ Boss: Poll trạng thái chết của Boss để kích hoạt Victory
        if (isBossMode && !victoryTriggered && bossController != null && bossController.isDead)
        {
            TriggerVictory();
        }
    }

    /// <summary>
    /// Chế độ Trigger Zone (cuối Map): Khi Player bước vào vùng Collider sẽ kích hoạt Victory.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isBossMode) return; // Bỏ qua Trigger Zone nếu đang ở chế độ Boss

        if (collision.CompareTag("Player"))
        {
            TriggerVictory();
        }
    }
    #endregion

    #region CORE LOGIC
    /// <summary>
    /// Hàm cốt lõi xử lý Victory. Có thể được gọi từ OnTriggerEnter2D (Zone) hoặc Update (Boss Mode).
    /// Thực hiện: Lưu Game → Chuyển GameState sang Victory.
    /// </summary>
    public void TriggerVictory()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;

        Debug.Log("<color=cyan>[VictoryTrigger] Boss đã bị tiêu diệt! Đang thực hiện Lưu Game và hiển thị Victory...</color>");

        // Bước 1: Lưu Game tự động
        PerformAutoSave();

        // Bước 2: Chuyển trạng thái sang Victory để UIManager hiển thị màn hình Victory
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Victory);
        }
        else
        {
            Debug.LogError("[VictoryTrigger] Không tìm thấy GameManager.Instance! Không thể chuyển sang trạng thái Victory.");
        }
    }

    /// <summary>
    /// Lưu Game tự động bằng cách thu thập dữ liệu từ Player và InventoryManager,
    /// sau đó ghi vào file thông qua SaveDataManager.
    /// </summary>
    private void PerformAutoSave()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();

        if (player != null && InventoryManager.Instance != null && SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CollectDataFromGame(player, InventoryManager.Instance);
            SaveDataManager.Instance.SaveGameToFile();
            Debug.Log("<color=green>[VictoryTrigger] Lưu Game tự động sau khi Boss chết: Thành công!</color>");
        }
        else
        {
            Debug.LogWarning("[VictoryTrigger] Auto-Save thất bại: Thiếu Player, InventoryManager, hoặc SaveDataManager.");
        }
    }
    #endregion
}