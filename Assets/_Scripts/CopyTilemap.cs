using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;

public class CopyTilemap : EditorWindow
{
    Tilemap source;
    Tilemap destination;

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
}
#endif