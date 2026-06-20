using UnityEngine;

/// <summary>
/// Vùng kích hoạt Chiến thắng (Win Zone).
/// Đặt tại cuối Map. Khi Player bước vào sẽ kích hoạt trạng thái GameState.Victory để hiện UI Win Game.
/// </summary>
public class VictoryTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Victory);
        }
    }
}