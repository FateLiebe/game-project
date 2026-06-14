using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<ItemSO> allItemsInGame = new List<ItemSO>();

    public ItemSO GetItemByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (ItemSO item in allItemsInGame) if (item != null && item.itemID == id) return item;
        return null;
    }
}