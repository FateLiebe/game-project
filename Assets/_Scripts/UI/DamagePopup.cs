using UnityEngine;
using TMPro; 

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private float moveYSpeed = 2f;

    // --- Biến cho hiệu ứng Level Up ---
    private bool isLevelUp;
    private float blinkSpeed = 25f; // Tốc độ nhấp nháy cực nhanh
    private Color colorYellow = Color.yellow;
    private Color colorOrange = new Color(1f, 0.5f, 0f); // Màu cam

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    // ==========================================
    // 1. CÀI ĐẶT POPUP SÁT THƯƠNG
    // ==========================================
    public void SetupDamage(float damageAmount, bool isCrit, bool isPlayer)
    {
        isLevelUp = false;
        
        // Làm tròn tối đa 3 chữ số sau dấu phẩy (vd: 10.123)
        string dmgString = damageAmount.ToString("0.###"); 

        if (isCrit)
        {
            textMesh.fontSize = 6f; 
            // Thẻ <i> để in nghiêng, thêm icon và dấu trừ
            textMesh.text = $"<i><sprite=0> -{dmgString}</i>";
            textColor = Color.yellow;
        }
        else
        {
            textMesh.fontSize = 4f;
            textMesh.text = $"-{dmgString}";
            // Nếu là Player bị đánh -> Màu đỏ, Quái bị đánh -> Màu Trắng
            textColor = isPlayer ? Color.red : Color.white; 
        }
        
        textMesh.color = textColor;
        disappearTimer = 0.8f; 
    }

    // ==========================================
    // 2. CÀI ĐẶT POPUP KINH NGHIỆM (EXP)
    // ==========================================
    public void SetupEXP(float expAmount)
    {
        isLevelUp = false;
        string expString = expAmount.ToString("0.###");
        
        textMesh.text = $"+{expString} EXP";
        textMesh.fontSize = 4.5f;
        textColor = Color.green; // Màu xanh lá
        textMesh.color = textColor;
        
        disappearTimer = 1f;
        moveYSpeed = 2f; 
    }

    // ==========================================
    // 3. CÀI ĐẶT POPUP LEVEL UP
    // ==========================================
    public void SetupLevelUp()
    {
        isLevelUp = true;
        textMesh.text = "LEVEL UP! ^";
        textMesh.fontSize = 5.5f;
        disappearTimer = 1.5f;
        moveYSpeed = 1.5f; 
    }

    // ==========================================
    // 4. POPUP CẢNH BÁO (màu đỏ, chữ tự do)
    // ==========================================
    public void SetupWarning(string message)
    {
        isLevelUp = false;
        textMesh.text = message;
        textMesh.fontSize = 4.5f;
        textColor = Color.red;
        textMesh.color = textColor;
        disappearTimer = 1.2f;
        moveYSpeed = 1.5f;
    }

    private void Update()
    {
        // Bay từ từ lên trên
        transform.position += new Vector3(0, moveYSpeed) * Time.deltaTime;

        // HIỆU ỨNG NHẤP NHÁY CHO LEVEL UP
        if (isLevelUp)
        {
            textMesh.color = Color.Lerp(colorYellow, colorOrange, Mathf.PingPong(Time.time * blinkSpeed, 1f));
        }

        // XỬ LÝ MỜ DẦN VÀ TỰ HỦY
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            float disappearSpeed = 3f;
            if (!isLevelUp)
            {
                textColor.a -= disappearSpeed * Time.deltaTime;
                textMesh.color = textColor;
                if (textColor.a < 0) Destroy(gameObject);
            }
            else
            {
                // Mờ dần cho cả 2 màu nhấp nháy
                colorYellow.a -= disappearSpeed * Time.deltaTime;
                colorOrange.a -= disappearSpeed * Time.deltaTime;
                
                Color currentColor = textMesh.color;
                currentColor.a = colorYellow.a;
                textMesh.color = currentColor;
                
                if (colorYellow.a < 0) Destroy(gameObject);
            }
        }
    }
}