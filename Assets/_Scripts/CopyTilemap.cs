using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Công cụ hỗ trợ trên Editor (Không chạy trong Game).
/// Chuyển đổi và gom (Merge) toàn bộ gạch (Tile) từ lớp Tilemap này sang Tilemap khác chỉ bằng 1 nút bấm.
/// </summary>
public class CopyTilemap : EditorWindow
{
    #region VARIABLES & PROPERTIES
    Tilemap source;
    Tilemap destination;
    #endregion

    #region UNITY EDITOR LOGIC
    [MenuItem("Tools/Copy Tilemap Tiles")]
    public static void ShowWindow()
    {
        GetWindow<CopyTilemap>("Copy Tilemap");
    }

    void OnGUI()
    {
        source = (Tilemap)EditorGUILayout.ObjectField(
            "Source (Decoration)", source, typeof(Tilemap), true);
        destination = (Tilemap)EditorGUILayout.ObjectField(
            "Destination (Ground)", destination, typeof(Tilemap), true);

        if (GUILayout.Button("Copy & Clear Source"))
        {
            if (source == null || destination == null)
            {
                Debug.LogError("Chọn đủ Source và Destination!");
                return;
            }

            BoundsInt bounds = source.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                TileBase tile = source.GetTile(pos);
                if (tile != null)
                {
                    destination.SetTile(pos, tile);
                    source.SetTile(pos, null); // xóa khỏi source
                }
            }
            Debug.Log("Done! Đã chuyển toàn bộ tile.");
        }
    }
    #endregion
}
#endif