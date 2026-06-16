using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections; // [BẮT BUỘC]: Để chạy được Coroutine (IEnumerator)

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("HUD")]
    public Slider hpSlider;
    public Image perfectDodgeCDBorder;

    [Header("Menus (Khác)")]
    public GameObject gameOverMenu;
    
    [Header("Pause UI (Tích hợp)")]
    public GameObject pauseScreen;       
    
    [Header("Cài đặt Âm thanh (UI)")]
    public Toggle audioToggle;
    public Slider volumeSlider;

    [Header("Scene Settings")]
    public string mainMenuSceneName = "Main_Menu";

    [Header("Victory UI")]
    public GameObject victoryMenu;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.GameOver)
            {
                GameManager.Instance.TogglePause();
            }
        }
    }

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        if (pauseScreen != null) pauseScreen.SetActive(newState == GameManager.GameState.Paused);
        if (gameOverMenu != null) gameOverMenu.SetActive(newState == GameManager.GameState.GameOver);
        if (victoryMenu != null) victoryMenu.SetActive(newState == GameManager.GameState.Victory);

        // [AUDIO] Phát âm thanh game over
        if (newState == GameManager.GameState.GameOver)
            AudioManager.Instance?.PlayGameOver();
    }

    public void UpdateDodgeCD(float currentTimer, float maxCooldown)
    {
        if (perfectDodgeCDBorder != null) perfectDodgeCDBorder.fillAmount = currentTimer / maxCooldown;
    }

    // ==========================================
    // CHỨC NĂNG CỦA CÁC NÚT BẤM (GẮN VÀO ONCLICK)
    // ==========================================

    public void ResumeGame() 
    { 
        AudioManager.Instance?.PlayUIClick(); // [AUDIO]
        if (GameManager.Instance != null) GameManager.Instance.TogglePause(); 
    }

    private void PerformSave()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null && InventoryManager.Instance != null && SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CollectDataFromGame(player, InventoryManager.Instance);
            SaveDataManager.Instance.SaveGameToFile();
            Debug.Log("<color=green>Đã Lưu Game thành công từ UI!</color>");
        }
        else
        {
            Debug.LogError("Không thể Lưu Game: Thiếu Player hoặc InventoryManager.");
        }
    }

    public void SaveGame() { PerformSave(); }

    public void SaveAndExit()
    {
        // 1. Lưu lại toàn bộ dữ liệu
        PerformSave();
        
        // 2. Mở khóa thời gian
        Time.timeScale = 1f; 
        
        // 3. Chốt State chuẩn bị về Menu
        if (GameManager.Instance != null) 
        {
            GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        }
        
        // 4. [BẢN VÁ TRIỆT ĐỂ]: Gọi Coroutine dọn dẹp các Map (Additive) rác trước khi về Menu
        StartCoroutine(UnloadMapsAndGoToMenu());
    }

    // Luồng dọn dẹp bộ nhớ an toàn
    private IEnumerator UnloadMapsAndGoToMenu()
    {
        // Vòng lặp ngược: an toàn tuyệt đối khi xóa phần tử
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            // Xóa sạch mọi thứ đang được nạp, ngoại trừ sườn Core_Gameplay
            if (s.name != "Core_Gameplay" && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Sau khi bộ nhớ đã sạch bóng, mới an tâm load về Main Menu
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ==========================================
    // KHU VỰC LOGIC GAMEOVER & VICTORY (GIAI ĐOẠN 4)
    // ==========================================

    public void LoadLastSave()
    {
        Time.timeScale = 1f;
        if (SaveDataManager.Instance != null) SaveDataManager.Instance.LoadGameFromFile();
        GameLoader.currentLoadMode = GameLoader.LoadMode.Respawn;
        StartCoroutine(CleanAndReloadGame());
    }

    public void QuitToMenuWithoutSave()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        StartCoroutine(UnloadMapsAndGoToMenu()); // Tái sử dụng hàm dọn dẹp đã viết ở Giai đoạn 3
    }

    private IEnumerator CleanAndReloadGame()
    {
        // Unload Map rác
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "Core_Gameplay" && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
        }
        
        // Gọi GameLoader nạp lại Map
        GameLoader loader = FindFirstObjectByType<GameLoader>();
        if (loader != null) loader.StartLoad();
    }

    // ==========================================
    // KHU VỰC LOGIC ÂM THANH (PauseScreen)
    // Kết nối Slider (1-10) và Toggle vào các hàm này
    // ==========================================
    
    /// <summary>Gắn vào OnValueChanged của Slider âm lượng (1-10)</summary>
    public void OnVolumeSliderChanged(float value)
    {
        AudioManager.Instance?.SetVolume(value);
    }

    /// <summary>Gắn vào OnValueChanged của Toggle bật/tắt âm thanh</summary>
    public void OnAudioToggleChanged(bool isOn)
    {
        AudioManager.Instance?.SetMute(!isOn); // isOn=true → không mute
    }

    /// <summary>Gắn vào OnClick của bất kỳ nút UI nào</summary>
    public void OnUIButtonClick()
    {
        AudioManager.Instance?.PlayUIClick();
    }
}