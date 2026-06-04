using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MapPortal : MonoBehaviour
{
    [Header("--- CÀI ĐẶT CỔNG CHUYỂN MAP ---")]
    public string nextMapName;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Khóa cổng để tránh chạm nhiều lần
            GetComponent<Collider2D>().enabled = false;
            
            // Truyền luôn cục Player vào để xử lý
            StartCoroutine(TransitionMap(other.gameObject));
        }
    }

    private IEnumerator TransitionMap(GameObject playerObj)
    {
        Debug.Log($"<color=yellow>Đang tải bản đồ: {nextMapName}</color>");
        
        // --- 1. ĐÓNG BĂNG PLAYER ---
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
        PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();
        
        // Tắt mô phỏng vật lý (chống rơi) và tắt điều khiển (chống bấm nút bậy)
        if (rb != null) rb.simulated = false; 
        if (playerCtrl != null) playerCtrl.enabled = false; 

        // --- 2. CHỜ TẢI MAP MỚI VÀ XÓA MAP CŨ ---
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextMapName, LoadSceneMode.Additive);
        yield return loadOp; // Đợi load xong 100%

        Scene currentMap = gameObject.scene; 
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentMap);
        yield return unloadOp; // Đợi xóa map cũ xong 100%

        // --- 3. RÃ ĐÔNG PLAYER ---
        // Đặt lại vận tốc về 0 để lỡ trước khi qua cổng đang phi nhanh cũng không bị kẹt tường
        if (rb != null) rb.linearVelocity = Vector2.zero; 
        
        // Bật lại vật lý và điều khiển
        if (rb != null) rb.simulated = true;
        if (playerCtrl != null) playerCtrl.enabled = true;
    }
}