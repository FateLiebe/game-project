using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object Pool tổng quát — tái sử dụng GameObject thay vì Instantiate/Destroy.
/// Cách dùng: ObjectPoolManager.Instance.Get(prefab, pos, rot)
/// Trả lại:   GetComponent&lt;PooledObject&gt;().ReturnToPool()
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    // Key = prefab InstanceID, Value = stack các bản inactive đang chờ tái sử dụng
    private readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // ==========================================
    #region PUBLIC API
    // ==========================================

    /// <summary>
    /// Lấy một instance từ pool (hoặc tạo mới nếu pool trống).
    /// PooledObject component được tự động gắn vào lần đầu tiên.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int id = prefab.GetInstanceID();
        EnsurePool(id);

        GameObject obj;
        if (_pools[id].Count > 0)
        {
            obj = _pools[id].Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.SetParent(null);
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(prefab, position, rotation);
            // Gắn PooledObject để object biết mình thuộc pool nào
            var po = obj.AddComponent<PooledObject>();
            po.prefabId = id;
        }

        return obj;
    }

    /// <summary>Trả object về pool. Thường gọi qua PooledObject.ReturnToPool().</summary>
    public void Return(int prefabId, GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform); // Giữ gọn dưới ObjectPoolManager
        EnsurePool(prefabId);
        _pools[prefabId].Enqueue(obj);
    }

    /// <summary>Khởi tạo sẵn N bản trong pool để tránh giật ở frame đầu tiên.</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        int id = prefab.GetInstanceID();
        EnsurePool(id);
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(prefab, transform);
            var po  = obj.AddComponent<PooledObject>();
            po.prefabId = id;
            Return(id, obj);
        }
    }

    #endregion

    private void EnsurePool(int id)
    {
        if (!_pools.ContainsKey(id))
            _pools[id] = new Queue<GameObject>();
    }
}
