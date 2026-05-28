using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsUIManager : MonoBehaviour
{
    // [MỚI THÊM] Singleton để các script khác dễ dàng gọi tới
    public static StatsUIManager Instance { get; private set; } 
    public bool IsOpen => uiPanel != null && uiPanel.activeSelf;

    [Header("References")]
    [SerializeField] private BaseEntity player;
    [SerializeField] private GameObject uiPanel;

    [Header("--- LEVEL & EXP UI ---")]
    [SerializeField] private TextMeshProUGUI txtLevel;    
    [SerializeField] private TextMeshProUGUI txtExp;      
    [SerializeField] private Image imgExpFill;            

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI txtStatPoints;
    [SerializeField] private TextMeshProUGUI txtHP;
    [SerializeField] private TextMeshProUGUI txtATK;
    [SerializeField] private TextMeshProUGUI txtDEF;
    [SerializeField] private TextMeshProUGUI txtCRIT;
    [SerializeField] private TextMeshProUGUI txtCritDamage;
    [SerializeField] private TextMeshProUGUI txtSpeed;

    [Header("Buttons")]
    [SerializeField] private Button btnAddHP;
    [SerializeField] private Button btnAddATK;
    [SerializeField] private Button btnAddDEF;
    [SerializeField] private Button btnAddCRIT;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (btnAddHP != null) btnAddHP.onClick.AddListener(() => UpgradeStat("HP"));
        if (btnAddATK != null) btnAddATK.onClick.AddListener(() => UpgradeStat("ATK"));
        if (btnAddDEF != null) btnAddDEF.onClick.AddListener(() => UpgradeStat("DEF"));
        if (btnAddCRIT != null) btnAddCRIT.onClick.AddListener(() => UpgradeStat("CRIT"));

        if (uiPanel != null) uiPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            // [LOGIC CHẶN]: Đang ngoài giao diện mà bấm B, nhưng lại đang combat -> Chặn!
            if (!IsOpen && player != null && player.IsInCombat)
            {
                Debug.Log("<color=red>Đang trong giao tranh, không thể mở túi đồ!</color>");
                // Gợi ý: Có thể gọi hàm DamagePopup để hiện chữ "Đang giao tranh!" trôi nổi ở đây
                return; 
            }

            ToggleUI();
        }

        if (IsOpen) UpdateUI();
    }

    public void ToggleUI()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(!uiPanel.activeSelf);
            if (uiPanel.activeSelf) UpdateUI();

            // Nếu tắt túi đi thì dập luôn Tooltip
            else if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
        }
    }

    // [MỚI THÊM] Hàm dùng để gọi khi click ra ngoài UI
    public void CloseUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);

        // Click ra ngoài viền đen tự tắt túi thì tắt luôn Tooltip
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }

    private void UpgradeStat(string statType)
    {
        if (player != null && player.currentStatPoints > 0)
        {
            player.AllocateStatPoint(statType); 
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        if (player == null) return;

        if (txtLevel != null) txtLevel.text = player.currentLevel.ToString();
        if (txtExp != null) txtExp.text = $"EXP: {player.currentEXP} / {player.expToNextLevel}";
        if (imgExpFill != null) imgExpFill.fillAmount = player.currentEXP / player.expToNextLevel;

        txtStatPoints.text = $"Điểm tiềm năng: {player.currentStatPoints}";
        txtHP.text = $"Máu: {player.currentHealth} / {player.MaxHealth}";
        txtATK.text = $"Tấn công: {player.Attack}";
        txtDEF.text = $"Phòng thủ: {player.Defense}";
        txtCRIT.text = $"Tỉ lệ bạo kích: {player.CritRate}%";
        if (txtCritDamage != null) txtCritDamage.text = $"Sát thương bạo kích: {player.CritDamage}%"; 
        if (txtSpeed != null) txtSpeed.text = $"Tốc độ: {player.Speed}";

        // Ẩn nút nếu hết điểm
        bool hasPoints = player.currentStatPoints > 0;
        if (btnAddHP != null) btnAddHP.gameObject.SetActive(hasPoints);
        if (btnAddATK != null) btnAddATK.gameObject.SetActive(hasPoints);
        if (btnAddDEF != null) btnAddDEF.gameObject.SetActive(hasPoints);
        if (btnAddCRIT != null) btnAddCRIT.gameObject.SetActive(hasPoints);
    }
}