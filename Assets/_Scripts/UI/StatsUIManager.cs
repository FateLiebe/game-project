using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseEntity player;
    [SerializeField] private GameObject uiPanel;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI txtStatPoints;
    [SerializeField] private TextMeshProUGUI txtHP;
    [SerializeField] private TextMeshProUGUI txtATK;
    [SerializeField] private TextMeshProUGUI txtDEF;
    [SerializeField] private TextMeshProUGUI txtCRIT;

    [Header("Buttons")]
    [SerializeField] private Button btnAddHP;
    [SerializeField] private Button btnAddATK;
    [SerializeField] private Button btnAddDEF;
    [SerializeField] private Button btnAddCRIT;

    private void Start()
    {
        btnAddHP.onClick.AddListener(() => UpgradeStat("HP"));
        btnAddATK.onClick.AddListener(() => UpgradeStat("ATK"));
        btnAddDEF.onClick.AddListener(() => UpgradeStat("DEF"));
        btnAddCRIT.onClick.AddListener(() => UpgradeStat("CRIT"));

        if (uiPanel != null) uiPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            bool isActive = !uiPanel.activeSelf;
            uiPanel.SetActive(isActive);
            if (isActive) UpdateUI();
        }
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

        txtStatPoints.text = $"Điểm tiềm năng: {player.currentStatPoints}";
        txtHP.text = $"Máu tối đa: {player.MaxHealth}";
        txtATK.text = $"Tấn công: {player.Attack}";
        txtDEF.text = $"Phòng thủ: {player.Defense}";
        txtCRIT.text = $"Tỉ lệ bạo kích: {player.CritRate}%";

        // Ẩn nút nếu hết điểm
        bool hasPoints = player.currentStatPoints > 0;
        btnAddHP.gameObject.SetActive(hasPoints);
        btnAddATK.gameObject.SetActive(hasPoints);
        btnAddDEF.gameObject.SetActive(hasPoints);
        btnAddCRIT.gameObject.SetActive(hasPoints);
    }
}