using UnityEngine;

public class SaveStation : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerController player = collision.GetComponent<PlayerController>();
            InventoryManager inv = FindFirstObjectByType<InventoryManager>();

            if (player != null && inv != null && SaveDataManager.Instance != null)
            {
                SaveDataManager.Instance.SaveAtCheckpoint(player, inv, transform);
                Debug.Log("<color=green>ĐÃ ĐẾN TRẠM CHECKPOINT! AUTO-SAVE THÀNH CÔNG!</color>");
            }
        }
    }
}