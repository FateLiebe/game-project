using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Xử lý quá trình chuyển cảnh (Load Map). 
/// Đóng vai trò dọn dẹp bộ nhớ (Unload map cũ, diệt EventSystem dư thừa) và đồng bộ tọa độ/dữ liệu cho Player ngay khi vừa load xong.
/// </summary>
public class GameLoader : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public string firstMapName = "Map_1";

    public enum LoadMode { NewGame, Continue, Respawn }
    public static LoadMode currentLoadMode = LoadMode.NewGame;
    #endregion

    #region UNITY LIFECYCLE
    private IEnumerator Start() { yield return StartCoroutine(LoadRoutine()); }
    #endregion

    #region PUBLIC METHODS
    public void StartLoad() { StartCoroutine(LoadRoutine()); }
    #endregion

    #region COROUTINES
    /// <summary>
    /// Luồng xử lý nạp không gian: Tự động phân luồng theo Save/Load hoặc Checkpoint.
    /// Dọn sạch EventSystem rác khi dùng Additive Load để tránh lỗi dội âm thanh/UI.
    /// </summary>
    private IEnumerator LoadRoutine()
    {
        if (UIManager.Instance != null && UIManager.Instance.loadingScreen != null)
        {
            // Bật đen NGAY LẬP TỨC để che đi khoảnh khắc Main Menu vừa bị hủy và Core_Gameplay vừa xuất hiện
            UIManager.Instance.loadingScreen.alpha = 1f;
            UIManager.Instance.loadingScreen.gameObject.SetActive(true);
        }

        string mapToLoad = firstMapName;

        // 1. CHỌN TÊN MAP DỰA TRÊN CHẾ ĐỘ LOAD
        if (SaveDataManager.Instance != null && SaveDataManager.Instance.currentData != null)
        {
            if (currentLoadMode == LoadMode.Continue) 
                mapToLoad = SaveDataManager.Instance.currentData.currentSceneName; // Load chỗ Save tay
            else if (currentLoadMode == LoadMode.Respawn) 
                mapToLoad = SaveDataManager.Instance.currentData.checkSceneName;   // Load chỗ Checkpoint
        }

        // 2. DỌN RÁC
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "Core_Gameplay" && s.isLoaded) { yield return SceneManager.UnloadSceneAsync(s); i--; }
        }

        // 3. LOAD MAP
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(mapToLoad, LoadSceneMode.Additive);
        yield return loadOp;
        yield return null;

        // Dọn dẹp các EventSystem lọt vào qua quá trình LoadSceneAdditive để tránh cảnh báo.
        UnityEngine.EventSystems.EventSystem[] eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        foreach (var es in eventSystems)
        {
            // Chỉ giữ lại EventSystem của Core_Gameplay, xóa các EventSystem lọt vào từ Map hoặc Main Menu
            if (es.gameObject.scene.name != "Core_Gameplay" && es.gameObject.scene.name != "DontDestroyOnLoad")
            {
                Destroy(es.gameObject);
            }
        }

        AudioManager.Instance?.RestartAmbientCycle();

        // 4. ÁP DỤNG DỮ LIỆU HOẶC SPAWN TÂN THỦ
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        PlayerController player = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
        InventoryManager inv = FindAnyObjectByType<InventoryManager>();

        if (player != null)
        {
            Vector3 oldPos = player.transform.position;

            if (currentLoadMode != LoadMode.NewGame && SaveDataManager.Instance != null && inv != null)
            {
                SaveDataManager.Instance.ApplyLoadedDataToPlayer(player, inv);
                UIManager.Instance?.SyncAudioUI(); // Đồng bộ Slider/Toggle với settings đã load
            }
            else
            {
                MapSpawnPoint spawnPoint = FindAnyObjectByType<MapSpawnPoint>();
                if (spawnPoint != null) 
                {
                    Vector3 spawnPos = spawnPoint.transform.position;
                    // Bắn tia Raycast để tìm mặt đất (tránh lỗi Player rơi tự do đầu game)
                    RaycastHit2D hit = Physics2D.Raycast(spawnPos, Vector2.down, 15f, LayerMask.GetMask("Ground"));
                    if (hit.collider != null)
                    {
                        Collider2D playerCol = player.GetComponent<Collider2D>();
                        if (playerCol != null)
                        {
                            float pivotToBottom = player.transform.position.y - playerCol.bounds.min.y;
                            spawnPos.y = hit.point.y + pivotToBottom + 0.05f;
                        }
                    }
                    player.transform.position = spawnPos;
                }
            }
            
            // Cắt đuôi Camera (Snap Camera) triệt để (Sử dụng delta position thay vì absolute position)
            Vector3 delta = player.transform.position - oldPos;
            Unity.Cinemachine.CinemachineCamera cam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (cam != null && delta.sqrMagnitude > 0.01f)
            {
                cam.OnTargetObjectWarped(player.transform, delta); 
                cam.PreviousStateIsValid = false;
            }
            
            // Yêu cầu tất cả Boss trong cảnh đồng bộ lại cấp độ với Player vừa được load
            // (Ngăn chặn tình trạng Boss lấy cấp độ khởi điểm lv1 do Start() chạy trước khi Player được ốp dữ liệu save).
            BossController[] bosses = FindObjectsByType<BossController>(FindObjectsSortMode.None);
            foreach (var boss in bosses)
            {
                boss.SyncLevelWithPlayer();
            }
        }

        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Gameplay);

        // Chờ 1 frame cuối cùng để Camera và Physics đồng bộ vị trí hoàn toàn trước khi sáng màn hình
        yield return null;

        if (UIManager.Instance != null)
        {
            // Mở màn hình sáng lên để cho phép người chơi bắt đầu
            yield return UIManager.Instance.FadeLoadingScreen(false);
        }
    }
    #endregion
}
