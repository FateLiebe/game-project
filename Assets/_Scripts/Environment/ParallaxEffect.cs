using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Header("Tỷ lệ dịch chuyển (0 = Gắn chặt vào Map, 1 = Đi theo Camera)")]
    public Vector2 parallaxMultiplier;

    private Transform cameraTransform;
    
    // Chỉ cần duy nhất 1 mỏ neo: Vị trí gốc của Bức ảnh
    private Vector3 startPos;     

    private void Start()
    {
        cameraTransform = Camera.main.transform;
        
        // Lưu lại vị trí của bức ảnh lúc bạn vừa đặt trong Scene
        startPos = transform.position; 
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        // 1. Tính khoảng cách TUYỆT ĐỐI từ Camera đến mỏ neo
        Vector3 travel = cameraTransform.position - startPos;

        // 2. Vị trí mới = Mỏ neo + (Khoảng cách * Tỷ lệ)
        float newX = startPos.x + (travel.x * parallaxMultiplier.x);
        float newY = startPos.y + (travel.y * parallaxMultiplier.y);

        // 3. Gán thẳng vị trí mới. Không cộng dồn, không cần biết frame trước Camera ở đâu!
        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}