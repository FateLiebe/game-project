using UnityEngine;

/// <summary>
/// Định danh Object Pool. Tự động được ObjectPoolManager gắn lên các vật thể ném vào kho.
/// Giúp vật thể nhớ được ID nguồn gốc của mình và tự biết cách quay về đúng ngăn chứa khi gọi hàm ReturnToPool.
/// </summary>
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [HideInInspector] public int prefabId;
    #endregion

    #region PUBLIC METHODS
    public void ReturnToPool()
    {
        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Return(prefabId, gameObject);
        else
            Destroy(gameObject); // Fallback an toàn nếu pool chưa tồn tại
    }
    #endregion
}
