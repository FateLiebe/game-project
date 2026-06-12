using UnityEngine;

public class OutlineFollower : MonoBehaviour
{
    public SpriteRenderer mainRenderer;
    private SpriteRenderer outlineRenderer;

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
}