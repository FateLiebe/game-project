using UnityEngine;
using System;

/// <summary>
/// Quản lý vòng đời và các trạng thái cốt lõi của trò chơi (Menu, Đang chơi, Tạm dừng, Chết).
/// Sử dụng mô hình Singleton và sống xuyên suốt các Scene (DontDestroyOnLoad).
/// </summary>
public class GameManager : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static GameManager Instance { get; private set; }

    // Định nghĩa 6 trạng thái cốt lõi của trò chơi
    public enum GameState { MainMenu, Loading, Gameplay, Paused, GameOver, Victory }
    public GameState CurrentState { get; private set; }

    // Event này dùng để báo cho UIManager, Audio, Enemy... biết mỗi khi trạng thái Game thay đổi
    public event Action<GameState> OnGameStateChanged;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Sống dai dẳng xuyên suốt mọi Map
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Khởi động ở MainMenu. GameLoader sẽ chủ động gọi ChangeState(Gameplay)
        ChangeState(GameState.MainMenu); 
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Chuyển đổi trạng thái game. Kèm theo đó là can thiệp vào Time.timeScale (Đóng băng thời gian khi Pause/Victory, Làm chậm khi chết).
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;

        // Xử lý cơ chế Thời gian (Time.timeScale) dùng chung cho toàn game
        switch (newState)
        {
            case GameState.MainMenu:
            case GameState.Loading:
            case GameState.Gameplay:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
            case GameState.Victory:
                Time.timeScale = 0f; // Đóng băng mọi hoạt động vật lý/animation
                break;
            case GameState.GameOver:
                Time.timeScale = 0.5f; // Hiệu ứng Slow-motion khi chết cho kịch tính
                break;
        }

        // Bắn tín hiệu cho các script khác biết trạng thái vừa đổi
        OnGameStateChanged?.Invoke(newState);
        
        Debug.Log($"<color=orange>[GameManager] Trạng thái Game đã chuyển sang: {newState}</color>");
    }
    #endregion

    #region UI BUTTON HANDLERS
    public void TogglePause()
    {
        if (CurrentState == GameState.Gameplay) ChangeState(GameState.Paused);
        else if (CurrentState == GameState.Paused) ChangeState(GameState.Gameplay);
    }

    public void GameOver()
    {
        if (CurrentState != GameState.GameOver) ChangeState(GameState.GameOver);
    }
    #endregion
}