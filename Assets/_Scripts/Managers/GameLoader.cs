using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameLoader : MonoBehaviour
{
    public string firstMapName = "Map_1";

    // KHAI BÁO ENUM MỚI ĐỂ CHỮA LỖI CS0117
    public enum LoadMode { NewGame, Continue, Respawn }
    public static LoadMode currentLoadMode = LoadMode.NewGame;

    private IEnumerator Start() { yield return StartCoroutine(LoadRoutine()); }
    public void StartLoad() { StartCoroutine(LoadRoutine()); }

    private IEnumerator LoadRoutine()
    {
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

        // 4. ÁP DỤNG DỮ LIỆU HOẶC SPAWN TÂN THỦ
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        PlayerController player = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
        InventoryManager inv = FindFirstObjectByType<InventoryManager>();

        if (player != null)
        {
            if (currentLoadMode != LoadMode.NewGame && SaveDataManager.Instance != null && inv != null)
                SaveDataManager.Instance.ApplyLoadedDataToPlayer(player, inv);
            else
            {
                MapSpawnPoint spawnPoint = FindFirstObjectByType<MapSpawnPoint>();
                if (spawnPoint != null) player.transform.position = spawnPoint.transform.position;
            }
        }

        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Gameplay);
    }
}