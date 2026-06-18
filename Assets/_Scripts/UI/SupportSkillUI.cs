using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [PHASE 3] Subscribe vào PlayerController.OnSupportSkillUpdated thay vì bị gọi trực tiếp.
/// </summary>
public class SupportSkillUI : MonoBehaviour
{
    public static SupportSkillUI Instance;

    public Image iconImage;
    public Image cdOverlay;
    public TextMeshProUGUI usesText;

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

    private void SetVisible(bool visible)
    {
        if (iconImage  != null) iconImage.gameObject.SetActive(visible);
        if (cdOverlay  != null) cdOverlay.gameObject.SetActive(visible);
        if (usesText   != null) usesText.gameObject.SetActive(visible);
    }
}