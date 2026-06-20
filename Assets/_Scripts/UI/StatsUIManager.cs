using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện Bảng Chỉ Số (Panel Mở bằng phím B hoặc nút trong Inventory).
/// Bao gồm hiển thị Level, EXP, và cộng điểm (Stats).
/// </summary>
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

    // [COMBAT WARNING] anti-spam
    private float lastCombatWarningTime = -99f;
    private const float COMBAT_WARNING_COOLDOWN = 2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (btnAddHP   != null) btnAddHP.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); UpgradeStat("HP"); });
        if (btnAddATK  != null) btnAddATK.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); UpgradeStat("ATK"); });
        if (btnAddDEF  != null) btnAddDEF.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); UpgradeStat("DEF"); });
        if (btnAddCRIT != null) btnAddCRIT.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); UpgradeStat("CRIT"); });

        if (uiPanel != null) uiPanel.SetActive(false);
    }

    /// <summary>
    /// Lắng nghe input phím B để mở Túi đồ.
    /// Nếu Player đang trong vùng giao tranh (InCombat), KHÔNG cho phép mở để tránh lạm dụng bơm máu/đổi vũ khí giữa chừng.
    /// </summary>
    private void Update()
    {
        // [CHẶN] Không xử lý input khi game đang Paused/GameOver/Victory
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
            return;

        if (Input.GetKeyDown(KeyCode.B))
        {
            if (!IsOpen && player != null && player.IsInCombat)
            {
                // [POPUP] Hiển thị cảnh báo bằng DamagePopup, chống spam 2s
                if (Time.time - lastCombatWarningTime >= COMBAT_WARNING_COOLDOWN)
                {
                    lastCombatWarningTime = Time.time;
                    if (player.damagePopupPrefab != null)
                    {
                        GameObject popup;
                        if (ObjectPoolManager.Instance != null)
                            popup = ObjectPoolManager.Instance.Get(player.damagePopupPrefab, player.transform.position + new Vector3(0, 1.8f, 0), Quaternion.identity);
                        else
                            popup = Instantiate(player.damagePopupPrefab, player.transform.position + new Vector3(0, 1.8f, 0), Quaternion.identity);
                        DamagePopup ps = popup.GetComponent<DamagePopup>();
                        if (ps != null) ps.SetupWarning("In combat!");
                    }
                }
                return;
            }

            if (ShopManager.Instance != null && ShopManager.Instance.isShopOpen)
            {
                ShopManager.Instance.CloseShop();
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
            if (uiPanel.activeSelf)
            {
                UpdateUI();
                AudioManager.Instance?.PlayInventoryOpen(); // [AUDIO]
                
                // Hiển thị số lượng vàng ở trạng thái Inventory bình thường
                if (ShopManager.Instance != null && !ShopManager.Instance.isShopOpen)
                {
                    ShopManager.Instance.UpdateCoinDisplay();
                    if (ShopManager.Instance.btnSellBuy != null) ShopManager.Instance.btnSellBuy.gameObject.SetActive(false);
                    if (ShopManager.Instance.txtGoldInventory != null) ShopManager.Instance.txtGoldInventory.gameObject.SetActive(true);
                }
            }
            else
            {
                AudioManager.Instance?.PlayInventoryClose(); // [AUDIO]
                if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
            }
        }
    }

    // [MỚI THÊM] Hàm dùng để gọi khi click ra ngoài UI
    public void CloseUI()
    {
        if (uiPanel != null && uiPanel.activeSelf)
        {
            uiPanel.SetActive(false);
            AudioManager.Instance?.PlayInventoryClose(); // [AUDIO]
        }
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

        txtStatPoints.text = $"Stat Points: {player.currentStatPoints}";
        txtHP.text = $"Health: {player.currentHealth} / {player.MaxHealth}";
        txtATK.text = $"Attack: {player.Attack}";
        txtDEF.text = $"Defense: {player.Defense}";
        txtCRIT.text = $"Crit Rate: {player.CritRate}%";
        if (txtCritDamage != null) txtCritDamage.text = $"Crit Damage: {player.CritDamage}%"; 
        if (txtSpeed != null) txtSpeed.text = $"Speed: {player.Speed}";

        // Ẩn nút nếu hết điểm
        bool hasPoints = player.currentStatPoints > 0;
        if (btnAddHP != null) btnAddHP.gameObject.SetActive(hasPoints);
        if (btnAddATK != null) btnAddATK.gameObject.SetActive(hasPoints);
        if (btnAddDEF != null) btnAddDEF.gameObject.SetActive(hasPoints);
        if (btnAddCRIT != null) btnAddCRIT.gameObject.SetActive(hasPoints);
    }
}