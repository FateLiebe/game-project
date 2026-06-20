using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ thống tối ưu hóa bộ nhớ (Object Pool) cốt lõi. 
/// Thay vì dùng Instantiate/Destroy liên tục gây rác bộ nhớ (Garbage Collection giật lag), hệ thống này "tái chế" các GameObject (như tia chưởng, số dame) bằng cách bật/tắt (SetActive).
/// 
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
    /// Lấy một Object từ kho (hoặc tạo mới nếu kho đã rỗng).
    /// PooledObject component được tự động gắn vào để Object "nhớ" mình thuộc kho nào.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int id = prefab.GetHashCode(); // Sửa lỗi obsolete thay cho GetInstanceID()
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
        int id = prefab.GetHashCode(); // Sửa lỗi obsolete thay cho GetInstanceID()
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
