using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Giao diện Kỹ Năng Hỗ Trợ (Support Skill) trên màn hình chính (HUD).
/// Lắng nghe sự kiện OnSupportSkillUpdated từ PlayerController để cập nhật thanh Cooldown (Overlay) và số lượt dùng còn lại.
/// </summary>
public class SupportSkillUI : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static SupportSkillUI Instance;

    public Image iconImage;
    public Image cdOverlay;
    public TextMeshProUGUI usesText;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        Instance = this;
        // Ẩn UI ban đầu
        SetVisible(false);
    }

    private void Start()
    {
        // Subscribe vào event của Player
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnSupportSkillUpdated += UpdateUI;
        else
            // Trường hợp PlayerController chưa tồn tại khi Start (rare) — fallback
            Debug.LogWarning("[SupportSkillUI] PlayerController.Instance is null on Start.");
    }

    private void OnDestroy()
    {
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnSupportSkillUpdated -= UpdateUI;
    }
    #endregion

    #region PUBLIC METHODS
    public void UpdateUI(ItemSO skill, float currentCD, int usesLeft)
    {
        if (skill == null || usesLeft <= 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        iconImage.sprite = skill.icon;
        cdOverlay.fillAmount = currentCD > 0 ? currentCD / skill.skillCooldown : 0f;
        usesText.text = usesLeft.ToString();
    }
    #endregion

    #region PRIVATE METHODS
    private void SetVisible(bool visible)
    {
        if (iconImage  != null) iconImage.gameObject.SetActive(visible);
        if (cdOverlay  != null) cdOverlay.gameObject.SetActive(visible);
        if (usesText   != null) usesText.gameObject.SetActive(visible);
    }
    #endregion
}