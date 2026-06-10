using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("HUD")]
    public Slider hpSlider;
    public Image perfectDodgeCDBorder;

    [Header("Menus")]
    public GameObject pauseMenu;
    public GameObject gameOverMenu; // <-- Biến bị thiếu làm báo lỗi đây!

    private bool isPaused = false;
    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Khi vừa load game/load màn, đặt mọi thứ về bình thường
        isPaused = false;
        isGameOver = false;
        Time.timeScale = 1f; 

        // Ép ẩn Menu đi (chữa cái lỗi ấn Restart xong Menu vẫn hiện)
        if (pauseMenu != null) pauseMenu.SetActive(false);
        if (gameOverMenu != null) gameOverMenu.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !isGameOver) TogglePause();
    }

    public void UpdateDodgeCD(float currentTimer, float maxCooldown)
    {
        if (perfectDodgeCDBorder != null)
        {
            perfectDodgeCDBorder.fillAmount = currentTimer / maxCooldown;
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (pauseMenu != null) pauseMenu.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f; 
    }

    // --- CÁC HÀM GẮN VÀO NÚT BẤM (BUTTONS) ---
    public void ResumeGame() { TogglePause(); }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Core_Gameplay"); // Load lại scene gốc, GameLoader sẽ tự load Map_1
    }

    public void QuitGame() 
    { 
        Application.Quit(); 
        Debug.Log("Đã bấm nút Thoát Game!"); 
    }
}