using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLoader : MonoBehaviour
{
    [Header("Bản đồ mặc định khi vào game")]
    public string firstMapName = "Map_1";

    // ĐỔI TỪ Start() SANG Awake()
    void Awake() 
    {
        // Ép tải Map ngay lập tức trước khi bất kỳ Object nào khác được phép hoạt động
        if (!SceneManager.GetSceneByName(firstMapName).isLoaded)
        {
            SceneManager.LoadScene(firstMapName, LoadSceneMode.Additive);
        }
    }
}