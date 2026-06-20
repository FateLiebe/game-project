using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Quản lý bật/tắt các Menu UI toàn cục (Pause, GameOver, Victory).
/// Xử lý các sự kiện click chuột như Resume, Save, Quit to Menu.
/// </summary>
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
        SyncAudioUI();

        // [PHASE 3] Subscribe vào event Player — không cần Player gọi UIManager trực tiếp nữa
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnDodgeCooldownChanged += UpdateDodgeCD;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;

        // [PHASE 3] Unsubscribe để tránh memory leak
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnDodgeCooldownChanged -= UpdateDodgeCD;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state == GameManager.GameState.GameOver) return;

            // ESC trong Inventory → đóng Inventory trước, rồi vào Pause
            if (StatsUIManager.Instance != null && StatsUIManager.Instance.IsOpen)
            {
                if (ShopManager.Instance != null && ShopManager.Instance.isShopOpen)
                {
                    ShopManager.Instance.CloseShop();
                }
                else
                {
                    StatsUIManager.Instance.CloseUI();
                }
                return;
            }

            GameManager.Instance.TogglePause();
        }

        // [CHẶN BÀN PHÍM] Khi PauseScreen đang mở, không để các phím B/E/Q... lọt qua
        // Các script khác nên check GameManager.CurrentState == Paused trước khi xử lý input
        // Cách đơn giản nhất: block input trong Update của StatsUIManager (đã làm bên dưới)
    }

    /// <summary>
    /// Tự động lắng nghe sự kiện từ GameManager để bật/tắt các màn hình tương ứng.
    /// Đảm bảo UI luôn đồng bộ tuyệt đối với logic của Game.
    /// </summary>
    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        if (pauseScreen != null) pauseScreen.SetActive(newState == GameManager.GameState.Paused);
        if (gameOverMenu != null) gameOverMenu.SetActive(newState == GameManager.GameState.GameOver);
        if (victoryMenu != null) victoryMenu.SetActive(newState == GameManager.GameState.Victory);

        if (newState == GameManager.GameState.GameOver)
            AudioManager.Instance?.PlayGameOver();
    }

    // [PHASE 3] Handler nhận tín hiệu từ Player event — private, không còn cần public
    private void UpdateDodgeCD(float currentTimer, float maxCooldown)
    {
        if (perfectDodgeCDBorder != null)
            perfectDodgeCDBorder.fillAmount = currentTimer / maxCooldown;
    }

    // ==========================================
    // NÚT BẤM
    // ==========================================

    public void ResumeGame() 
    { 
        AudioManager.Instance?.PlayUIClick();
        if (GameManager.Instance != null) GameManager.Instance.TogglePause(); 
    }

    private void PerformSave()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && InventoryManager.Instance != null && SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CollectDataFromGame(player, InventoryManager.Instance);
            SaveDataManager.Instance.SaveGameToFile();
            Debug.Log("<color=green>Đã Lưu Game thành công từ UI!</color>");
        }
        else Debug.LogError("Không thể Lưu Game: Thiếu Player hoặc InventoryManager.");
    }

    public void SaveGame()
    {
        AudioManager.Instance?.PlayUIClick();
        PerformSave();
    }

    public void SaveAndExit()
    {
        AudioManager.Instance?.PlayUIClick();
        PerformSave();
        Time.timeScale = 1f; 
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        StartCoroutine(UnloadMapsAndGoToMenu());
    }

    private IEnumerator UnloadMapsAndGoToMenu()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "Core_Gameplay" && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ==========================================
    // GAME OVER & VICTORY
    // ==========================================

    public void LoadLastSave()
    {
        AudioManager.Instance?.PlayUIClick();
        Time.timeScale = 1f;
        if (SaveDataManager.Instance != null) SaveDataManager.Instance.LoadGameFromFile();
        GameLoader.currentLoadMode = GameLoader.LoadMode.Respawn;
        StartCoroutine(CleanAndReloadGame());
    }

    public void QuitToMenuWithoutSave()
    {
        AudioManager.Instance?.PlayUIClick();
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        StartCoroutine(UnloadMapsAndGoToMenu());
    }

    private IEnumerator CleanAndReloadGame()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "Core_Gameplay" && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
        }
        GameLoader loader = FindAnyObjectByType<GameLoader>();
        if (loader != null) loader.StartLoad();
    }

    // ==========================================
    // AUDIO SETTINGS
    // ==========================================

    public void OnVolumeSliderChanged(float value) => AudioManager.Instance?.SetVolume(value);
    public void OnAudioToggleChanged(bool isOn)    => AudioManager.Instance?.SetMute(!isOn);
    public void OnUIButtonClick()                  => AudioManager.Instance?.PlayUIClick();

    /// <summary>Đồng bộ Slider và Toggle khớp với AudioManager. Gọi sau load save.</summary>
    public void SyncAudioUI()
    {
        if (AudioManager.Instance == null) return;
        volumeSlider?.SetValueWithoutNotify(AudioManager.Instance.masterSliderValue);
        audioToggle?.SetIsOnWithoutNotify(!AudioManager.Instance.isMuted);
    }
}
