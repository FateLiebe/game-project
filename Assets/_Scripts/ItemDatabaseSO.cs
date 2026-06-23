using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Thư viện Vật Phẩm Trung Tâm (Database).
/// Lưu trữ toàn bộ các mẫu ItemSO trong game. Khi Load Game bằng chuỗi ID chữ (VD: wp_sword_1), hệ thống sẽ đối chiếu vào đây để lấy ra đúng vật phẩm vật lý.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    #region VARIABLES & PROPERTIES
    public List<ItemSO> allItemsInGame = new List<ItemSO>();
    #endregion

    #region PUBLIC METHODS
    public ItemSO GetItemByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (ItemSO item in allItemsInGame) if (item != null && item.itemID == id) return item;
        return null;
    }
    #endregion
}