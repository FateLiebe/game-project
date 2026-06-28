using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Xử lý điểm dịch chuyển (Cổng không gian) giữa các Map. 
/// Đóng băng Player, load Map mới bất đồng bộ (Additive) rồi di chuyển Player tới tọa độ an toàn của cổng đích.
/// </summary>
public class MapPortal : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("--- THÔNG TIN CỔNG ---")]
    [Tooltip("Số thứ tự của cổng này")]
    public int portalID = 1;

    [Header("--- ĐÍCH ĐẾN ---")]
    public string nextMapName;
    [Tooltip("Số thứ tự của cổng bên Map kia mà Player sẽ chui ra")]
    public int destinationPortalID = 1;
    #endregion

    #region UNITY LIFECYCLE
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
    #endregion

    #region COROUTINES
    /// <summary>
    /// Chuỗi quy trình chuyển không gian: 
    /// 1. Đóng băng (Freeze) Player.
    /// 2. Tải Map mới lên đè vào (Additive).
    /// 3. Định vị tọa độ cổng tương ứng và vứt Player sang đó.
    /// 4. Hủy (Unload) Map cũ và Rã đông Player.
    /// </summary>
    private IEnumerator TransitionMapRoutine(GameObject playerObj, Scene currentMap)
    {
        Debug.Log($"<color=yellow>Đang tải không gian: {nextMapName}...</color>");
        
        //Báo cho toàn game biết đang Loading (để chặn các tương tác rác nếu có)
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Loading);

        // 1. CHUẨN BỊ (ĐÓNG BĂNG PLAYER VÀ ANIMATION)
        PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();

        // Lấy hướng di chuyển thực tế (để xử lý đúng khi người chơi Backdash ngược vào cổng)
        Vector2 enterDirection = Vector2.right;
        if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            enterDirection = rb.linearVelocity.x > 0 ? Vector2.right : Vector2.left;
        }
        else if (playerCtrl != null)
        {
            enterDirection = playerObj.transform.localScale.x >= 0 ? Vector2.right : Vector2.left;
        }

        // Ép nhân vật đứng im hoàn toàn và phát animation Idle ngay lập tức
        Animator anim = playerObj.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetFloat("speed", 0f);
            anim.SetBool("isGrounded", true);
            anim.SetBool("isFalling", false);
            
            // Dùng Rebind để ép Animator reset về trạng thái gốc (Idle) mà không cần quan tâm tên State
            anim.Rebind();
            anim.Update(0f);
        }

        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        if (playerCtrl != null)
        {
            // Gọi thẳng ForceHideBossUI() để đảm bảo BossUIManager nhận được tín hiệu reset UI.
            playerCtrl.ForceHideBossUI();
            playerCtrl.DisableHitbox();
            playerCtrl.enabled = false;
        }

        // 1.5. FADE IN MÀN HÌNH CHỜ
        if (UIManager.Instance != null)
        {
            yield return UIManager.Instance.FadeLoadingScreen(true);
        }

        // 2. TẢI MAP MỚI
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextMapName, LoadSceneMode.Additive);
        yield return loadOp; 

        // 3. ĐỊNH VỊ CỔNG ĐÍCH & DI CHUYỂN PLAYER
        MapPortal[] allPortals = FindObjectsByType<MapPortal>();
        foreach (var portal in allPortals)
        {
            if (portal.gameObject.scene != currentMap &&
                portal.portalID == this.destinationPortalID)
            {
                // Dịch chuyển player lệch khỏi tâm portal một chút theo phương ngang
                Vector3 newPos = portal.transform.position + (Vector3)(enterDirection * 3f);
                
                // Bắn tia Raycast xuống dưới để tìm mặt đất và đặt Player đúng vị trí chạm đất
                // Đảm bảo Layer "Ground" đang được dùng cho mặt đất trong project của bạn
                RaycastHit2D hit = Physics2D.Raycast(newPos, Vector2.down, 15f, LayerMask.GetMask("Ground"));
                if (hit.collider != null)
                {
                    Collider2D playerCol = playerObj.GetComponent<Collider2D>();
                    if (playerCol != null)
                    {
                        // Lấy khoảng cách từ tâm (Pivot) của nhân vật đến lòng bàn chân (cạnh dưới Collider)
                        float pivotToBottom = playerObj.transform.position.y - playerCol.bounds.min.y;
                        newPos.y = hit.point.y + pivotToBottom + 0.05f; // Chạm đất chính xác
                    }
                }

                Vector3 delta = newPos - playerObj.transform.position;
                playerObj.transform.position = newPos;

                // Sửa lỗi Camera trượt (Snap Camera ngay lập tức tới vị trí mới của Player)
                Unity.Cinemachine.CinemachineCamera cam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                if (cam != null)
                {
                    cam.OnTargetObjectWarped(playerObj.transform, delta);
                    cam.PreviousStateIsValid = false;
                }
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

        AudioManager.Instance?.RestartAmbientCycle(); // Reset lại vòng lặp nhạc nền (Ambient) khi đổi màn chơi

        // 6. FADE OUT MÀN HÌNH CHỜ
        if (UIManager.Instance != null)
        {
            yield return UIManager.Instance.FadeLoadingScreen(false);
        }

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
    #endregion
}
