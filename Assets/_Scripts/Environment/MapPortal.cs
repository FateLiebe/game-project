using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MapPortal : MonoBehaviour
{
    [Header("--- THÔNG TIN CỔNG ---")]
    [Tooltip("Số thứ tự của cổng này")]
    public int portalID = 1;

    [Header("--- ĐÍCH ĐẾN ---")]
    public string nextMapName;
    [Tooltip("Số thứ tự của cổng bên Map kia mà Player sẽ chui ra")]
    public int destinationPortalID = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<Collider2D>().enabled = false; 
            
            PlayerController playerCtrl = other.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.StartCoroutine(TransitionMapRoutine(other.gameObject, gameObject.scene));
            }
        }
    }

    private IEnumerator TransitionMapRoutine(GameObject playerObj, Scene currentMap)
    {
        Debug.Log($"<color=yellow>Đang tải không gian: {nextMapName}...</color>");
        
        //Báo cho toàn game biết đang Loading (để chặn các tương tác rác nếu có)
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Loading);

        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
        PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();

        Vector2 enterDirection = Vector2.right;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            enterDirection = rb.linearVelocity.normalized;
        }
        
        // 1. ĐÓNG BĂNG
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        if (playerCtrl != null) { playerCtrl.DisableHitbox(); playerCtrl.enabled = false; }

        // 2. TẢI MAP MỚI
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextMapName, LoadSceneMode.Additive);
        yield return loadOp; 

        // 3. ĐỊNH VỊ CỔNG ĐÍCH & DI CHUYỂN PLAYER
        MapPortal[] allPortals = FindObjectsByType<MapPortal>(FindObjectsSortMode.None);
        foreach (var portal in allPortals)
        {
            if (portal.gameObject.scene != currentMap &&
                portal.portalID == this.destinationPortalID)
            {
                // Dịch chuyển player lệch khỏi tâm portal một chút
                playerObj.transform.position = portal.transform.position + (Vector3)(enterDirection * 3f);
                break;
            }
        }

        // 4. TIÊU HỦY MAP CŨ
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentMap);
        yield return unloadOp;

        // 5. RÃ ĐÔNG
        if (rb != null) rb.simulated = true;
        if (playerCtrl != null)
        {
            playerCtrl.enabled = true;
            playerCtrl.ForceGroundedState();
        }

        AudioManager.Instance?.RestartAmbientCycle(); // [AUDIO] Reset ambient cycle khi vào map mới

        //Trả lại trạng thái Gameplay bình thường
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Gameplay);
        
        Debug.Log("<color=green>Chuyển không gian thành công!</color>");
    }

    private IEnumerator ReEnablePortal(Collider2D portalCollider)
    {
        yield return new WaitForSeconds(0.5f);

        if (portalCollider != null)
            portalCollider.enabled = true;
    }
}