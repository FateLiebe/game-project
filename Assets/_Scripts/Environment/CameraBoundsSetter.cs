using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Tự động cập nhật giới hạn Camera (Confiner) khi load vào một Map mới.
/// Gắn script này vào GameObject chứa PolygonCollider2D (Is Trigger) trong mỗi Map.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraBoundsSetter : MonoBehaviour
{
    private void Start()
    {
        // 1. Lấy Collider giới hạn của Map này
        Collider2D mapBounds = GetComponent<Collider2D>();
        
        // 2. Tìm Cinemachine Camera đang hoạt động trong game
        CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
        
        if (cam != null)
        {
            // 3. Lấy Confiner 2D của Camera và gán Collider mới vào
            CinemachineConfiner2D confiner = cam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null)
            {
                confiner.BoundingShape2D = mapBounds;
                
                // Xóa cache cũ để Confiner nhận diện khung map mới ngay lập tức
                confiner.InvalidateBoundingShapeCache();
            }
            else
            {
                Debug.LogWarning("Không tìm thấy CinemachineConfiner2D trên Camera!");
            }
        }
    }
}
