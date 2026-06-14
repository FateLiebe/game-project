using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Buttons")]
    public Button continueButton; 
    
    [Header("Scene Settings")]
    public string gameplaySceneName = "Core_Gameplay";

    private string saveFilePath;

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

    private void LoadGameplayScene()
    {
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Loading);
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Đã bấm Thoát Game ngoài Menu!");
        Application.Quit();
    }
}