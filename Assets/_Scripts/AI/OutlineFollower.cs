using UnityEngine;

/// <summary>
/// Hiệu ứng viền (Outline).
/// Tự động copy và đồng bộ hình ảnh (Sprite) cũng như trạng thái lật chiều (FlipX/Y) từ Sprite gốc của nhân vật/quái sang một Sprite phụ họa nằm phía sau.
/// </summary>
public class OutlineFollower : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public SpriteRenderer mainRenderer;
    private SpriteRenderer outlineRenderer;
    #endregion

    #region UNITY LIFECYCLE
    void Awake()
    {
        outlineRenderer = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if(mainRenderer == null) return;

        outlineRenderer.sprite = mainRenderer.sprite;
        outlineRenderer.flipX = mainRenderer.flipX;
        outlineRenderer.flipY = mainRenderer.flipY;
    }
    #endregion
}