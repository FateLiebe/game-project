using UnityEngine;

/// <summary>
/// Tự động gắn lên pooled objects bởi ObjectPoolManager.
/// Cho phép object tự biết cách trả về pool mà không cần tham chiếu prefab.
/// </summary>
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    [HideInInspector] public int prefabId;

    public void ReturnToPool()
    {
        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Return(prefabId, gameObject);
        else
            Destroy(gameObject); // Fallback an toàn nếu pool chưa tồn tại
    }
}
