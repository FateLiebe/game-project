using UnityEngine;
using TMPro; 

/// <summary>
/// Quản lý hiệu ứng Text nổi lên trên đầu nhân vật (Damage, EXP, Coin, Level Up, Cảnh báo).
/// Script này thường được gắn vào một Prefab và được sinh ra liên tục thông qua Object Pool.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;           // Thành phần Text 3D để hiển thị chữ
    private float disappearTimer;           // Bộ đếm thời gian trước khi Text bắt đầu mờ dần
    private Color textColor;                // Màu chủ đạo của Text
    private float moveYSpeed = 2f;          // Tốc độ bay lơ lửng lên trên

    // --- BIẾN DÀNH RIÊNG CHO HIỆU ỨNG LEVEL UP ---
    private bool isLevelUp;                 // Cờ đánh dấu xem Text này có phải là chữ Level Up không
    private float blinkSpeed = 25f;         // Tốc độ nhấp nháy cực nhanh giữa 2 màu
    private Color colorYellow = Color.yellow;
    private Color colorOrange = new Color(1f, 0.5f, 0f); // Màu cam

    private void Awake()
    {
        // Cache lại TextMeshPro để tối ưu hiệu suất
        textMesh = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// Được gọi mỗi khi Prefab này được tái sử dụng (Lôi ra từ Object Pool).
    /// Bắt buộc phải Reset lại toàn bộ thông số về mặc định để tránh dính dữ liệu cũ.
    /// </summary>
    private void OnEnable()
    {
        moveYSpeed      = 2f;
        isLevelUp       = false;
        disappearTimer  = 0f;
        colorYellow.a   = 1f; // Khôi phục độ mờ về 100% (không trong suốt)
        colorOrange.a   = 1f;
    }

    // ==========================================
    // 1. CÀI ĐẶT POPUP SÁT THƯƠNG
    // ==========================================
    public void SetupDamage(float damageAmount, bool isCrit, bool isPlayer)
    {
        isLevelUp = false;
        
        // Làm tròn tối đa 3 chữ số sau dấu phẩy (vd: 10.123). Nếu là số chẵn (vd 10.0) thì hiện là 10
        string dmgString = damageAmount.ToString("0.###"); 

        if (isCrit)
        {
            textMesh.fontSize = 6f; // Chữ to hơn nếu bạo kích
            // Thẻ <i> để in nghiêng, <sprite=0> để chèn icon bạo kích (nếu có Sprite Asset)
            textMesh.text = $"<i><sprite=0> -{dmgString}</i>";
            textColor = Color.yellow; // Chí mạng màu vàng
        }
        else
        {
            textMesh.fontSize = 4f;
            textMesh.text = $"-{dmgString}";
            // Nếu Player bị quái đánh -> Chữ Đỏ. Quái bị đánh -> Chữ Trắng
            textColor = isPlayer ? Color.red : Color.white; 
        }
        
        textMesh.color = textColor;
        disappearTimer = 0.8f; // Sau 0.8s thì bắt đầu bay hơi
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
        textColor = Color.green; // Điểm kinh nghiệm luôn màu xanh lá
        textMesh.color = textColor;
        
        disappearTimer = 1f; // Cho hiện lâu hơn sát thương 1 chút (1s)
        moveYSpeed = 2f; 
    }

    // ==========================================
    // 3. CÀI ĐẶT POPUP VÀNG (COIN)
    // ==========================================
    public void SetupCoin(int amount)
    {
        isLevelUp = false;
        // Chèn icon đồng vàng (<sprite name="Coin_0">) vào đuôi chuỗi
        textMesh.text = $"+{amount} <sprite name=\"Coin_0\">";
        textMesh.fontSize = 5f;
        textColor = new Color(1f, 0.84f, 0f); // Mã màu vàng Gold đặc trưng
        textMesh.color = textColor;
        
        disappearTimer = 1f;
        moveYSpeed = 2f; 
    }

    // ==========================================
    // 4. CÀI ĐẶT POPUP LEVEL UP
    // ==========================================
    public void SetupLevelUp()
    {
        isLevelUp = true; // Bật cờ để Update biết đường chạy hiệu ứng nhấp nháy
        textMesh.text = "LEVEL UP! ^";
        textMesh.fontSize = 5.5f;
        disappearTimer = 1.5f; // Chữ Level Up hiện rất lâu (1.5s)
        moveYSpeed = 1.5f;     // Bay lên rất chậm rãi
    }

    // ==========================================
    // 5. POPUP CẢNH BÁO TỰ DO
    // ==========================================
    public void SetupWarning(string message)
    {
        isLevelUp = false;
        textMesh.text = message;
        textMesh.fontSize = 4.5f;
        textColor = Color.red; // Cảnh báo luôn màu đỏ
        textMesh.color = textColor;
        disappearTimer = 1.2f;
        moveYSpeed = 1.5f;
    }

    private void Update()
    {
        // 1. DI CHUYỂN: Cứ mỗi khung hình lại nhích lên một chút theo trục Y
        transform.position += new Vector3(0, moveYSpeed) * Time.deltaTime;

        // 2. HIỆU ỨNG NHẤP NHÁY (Chỉ áp dụng cho chữ LEVEL UP)
        if (isLevelUp)
        {
            // Trộn (Lerp) qua lại liên tục giữa Vàng và Cam tạo hiệu ứng nhấp nháy đèn Neon
            textMesh.color = Color.Lerp(colorYellow, colorOrange, Mathf.PingPong(Time.time * blinkSpeed, 1f));
        }

        // 3. XỬ LÝ MỜ DẦN VÀ TỰ HỦY
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0) // Hết thời gian chờ -> Bắt đầu bay hơi
        {
            float disappearSpeed = 3f; // Tốc độ tụt Alpha
            if (!isLevelUp)
            {
                // Giảm dần giá trị Alpha (Độ đục) của màu sắc
                textColor.a -= disappearSpeed * Time.deltaTime;
                textMesh.color = textColor;
                
                // Trở nên hoàn toàn trong suốt -> Trả về hồ bơi
                if (textColor.a < 0) ReturnOrDestroy();
            }
            else
            {
                // Mờ dần cho cả 2 màu nhấp nháy cùng lúc
                colorYellow.a -= disappearSpeed * Time.deltaTime;
                colorOrange.a -= disappearSpeed * Time.deltaTime;
                
                Color currentColor = textMesh.color;
                currentColor.a = colorYellow.a; // Áp dụng độ đục cho màu thực tế
                textMesh.color = currentColor;
                
                if (colorYellow.a < 0) ReturnOrDestroy();
            }
        }
    }

    /// <summary>
    /// Hàm dọn dẹp. Cố gắng trả Prefab về ObjectPool để tái sử dụng.
    /// Nếu không có ObjectPool thì đành phải Destroy (tốn tài nguyên hơn).
    /// </summary>
    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}