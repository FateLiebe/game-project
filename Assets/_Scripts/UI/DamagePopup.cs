using UnityEngine;
using TMPro; 

/// <summary>
/// Hệ thống hiển thị Popup sát thương, kinh nghiệm, vàng và các thông báo trên đầu nhân vật.
/// Class này được thiết kế để kết hợp với Object Pool, tái sử dụng để tối ưu hiệu năng.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    private TextMeshPro textMesh;           // Text 3D hiển thị nội dung
    private float disappearTimer;           // Thời gian chờ trước khi chữ bắt đầu mờ đi
    private Color textColor;                // Màu sắc của nội dung
    private float moveYSpeed = 2f;          // Tốc độ bay nổi lên trên màn hình

    // --- BIẾN DÀNH RIÊNG CHO HIỆU ỨNG LEVEL UP ---
    private bool isLevelUp;                 // Cờ đánh dấu xem Text này có phải là chữ Level Up không
    private float blinkSpeed = 25f;         // Tốc độ nhấp nháy cực nhanh giữa 2 màu
    private Color colorYellow = Color.yellow;
    private Color colorOrange = new Color(1f, 0.5f, 0f); // Màu cam
    #endregion

    #region UNITY LIFECYCLE
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

    private void Update()
    {
        // 1. Di chuyển văn bản nhích dần lên trên theo trục Y
        transform.position += new Vector3(0, moveYSpeed) * Time.deltaTime;

        // 2. Xử lý hiệu ứng nhấp nháy đặc biệt (Chỉ dành cho Level Up)
        if (isLevelUp)
        {
            textMesh.color = Color.Lerp(colorYellow, colorOrange, Mathf.PingPong(Time.time * blinkSpeed, 1f));
        }

        // 3. Bắt đầu quá trình mờ dần và thu hồi khi hết thời gian chờ
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0) 
        {
            float disappearSpeed = 3f;
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
    #endregion

    #region SETUP METHODS
    /// <summary>
    /// Thiết lập thông số hiển thị cho Popup Sát thương (Combat).
    /// </summary>
    /// <param name="damageAmount">Lượng sát thương thực tế</param>
    /// <param name="isCrit">Có phải sát thương chí mạng không</param>
    /// <param name="isPlayer">Là sát thương gây ra lên Player (màu đỏ) hay lên quái (màu trắng)</param>
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

    /// <summary>
    /// Thiết lập thông số hiển thị cho Popup Kinh nghiệm (EXP).
    /// Mặc định sẽ có màu xanh lá và bay chậm hơn sát thương.
    /// </summary>
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

    /// <summary>
    /// Thiết lập thông số hiển thị cho Popup Tiền Vàng (Coin).
    /// Có tích hợp kèm thẻ <sprite> để vẽ icon đồng xu.
    /// </summary>
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

    /// <summary>
    /// Thiết lập thông số hiển thị khi nhân vật Thăng cấp.
    /// Áp dụng hiệu ứng nhấp nháy đèn Neon trong hàm Update.
    /// </summary>
    public void SetupLevelUp()
    {
        isLevelUp = true; // Bật cờ để Update biết đường chạy hiệu ứng nhấp nháy
        textMesh.text = "LEVEL UP! ^";
        textMesh.fontSize = 5.5f;
        disappearTimer = 1.5f; // Chữ Level Up hiện rất lâu (1.5s)
        moveYSpeed = 1.5f;     // Bay lên rất chậm rãi
    }

    /// <summary>
    /// Hiển thị các thông báo cảnh báo (VD: Hết Mana, Hết Đạn, Khóa kỹ năng).
    /// Mặc định dùng màu đỏ gắt.
    /// </summary>
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

    /// <summary>
    /// Hiển thị thông báo thân thiện cho các hoạt động hệ thống (VD: Tự động trang bị Bùa).
    /// Mặc định dùng màu xanh ngọc (Cyan).
    /// </summary>
    public void SetupNotification(string message)
    {
        isLevelUp = false;
        textMesh.text = message;
        textMesh.fontSize = 4f;
        textColor = Color.cyan; 
        textMesh.color = textColor;
        disappearTimer = 1.5f;
        moveYSpeed = 1.2f;
    }
    #endregion

    #region CORE LOGIC & PRIVATE METHODS
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
    #endregion
}