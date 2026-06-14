using UnityEngine;

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