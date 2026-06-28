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

    [Header("Loading UI")]
    public CanvasGroup mainMenuLoadingScreen;
    public float fadeDuration = 0.5f;
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
        GameLoader.currentLoadMode = GameLoader.LoadMode.NewGame; 
        StartCoroutine(LoadGameplaySceneRoutine());
    }

    public void ContinueGame()
    {
        if (SaveDataManager.Instance != null) SaveDataManager.Instance.LoadGameFromFile();
        GameLoader.currentLoadMode = GameLoader.LoadMode.Continue; 
        StartCoroutine(LoadGameplaySceneRoutine());
    }

    public void QuitGame()
    {
        Debug.Log("Đã bấm Thoát Game ngoài Menu!");
        Application.Quit();
    }
    #endregion

    #region PRIVATE METHODS
    private System.Collections.IEnumerator LoadGameplaySceneRoutine()
    {
        // Làm đen màn hình trước khi load
        if (mainMenuLoadingScreen != null)
        {
            float time = 0;
            mainMenuLoadingScreen.gameObject.SetActive(true);
            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                mainMenuLoadingScreen.alpha = Mathf.Lerp(0f, 1f, time / fadeDuration);
                yield return null;
            }
            mainMenuLoadingScreen.alpha = 1f;
        }

        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameManager.GameState.Loading);
        SceneManager.LoadScene(gameplaySceneName);
    }
    #endregion
}