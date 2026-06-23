using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Trình điều khiển giao diện Menu chính (Main Menu).
/// Kiểm tra xem file Save có tồn tại hay không để bật/tắt nút "Continue", đồng thời thiết lập cờ LoadMode cho GameLoader.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("UI Buttons")]
    public Button continueButton; 
    
    [Header("Scene Settings")]
    public string gameplaySceneName = "Core_Gameplay";

    private string saveFilePath;
    #endregion

    #region UNITY LIFECYCLE
    private void Start()
    {
        Time.timeScale = 1f;
        
        if (GameManager.Instance != null) 
        {
            GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        }

        // Kiểm tra File Save để bật/tắt nút Continue
        saveFilePath = Path.Combine(Application.persistentDataPath, "saveData_slot1.json");
        if (continueButton != null)
        {
            continueButton.interactable = File.Exists(saveFilePath);
        }
    }
    #endregion

    #region PUBLIC METHODS
    public void StartNewGame()
    {
        if (SaveDataManager.Instance != null) SaveDataManager.Instance.NewGame();
        GameLoader.currentLoadMode = GameLoader.LoadMode.NewGame; // SỬA DÒNG NÀY
        LoadGameplayScene();
    }

    public void ContinueGame()
    {
        if (SaveDataManager.Instance != null) SaveDataManager.Instance.LoadGameFromFile();
        GameLoader.currentLoadMode = GameLoader.LoadMode.Continue; // SỬA DÒNG NÀY
        LoadGameplayScene();
    }

    public void QuitGame()
    {
        Debug.Log("Đã bấm Thoát Game ngoài Menu!");
        Application.Quit();
    }
    #endregion

    #region PRIVATE METHODS
    private void LoadGameplayScene()
    {
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Loading);
        SceneManager.LoadScene(gameplaySceneName);
    }
    #endregion
}