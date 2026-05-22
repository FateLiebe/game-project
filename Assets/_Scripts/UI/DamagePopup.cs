using UnityEngine;
using TMPro; // Bắt buộc phải có để dùng TextMeshPro

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private float moveYSpeed = 2f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    // Cài đặt cho số Sát Thương
    public void Setup(float damageAmount, bool isCrit)
    {
        textMesh.text = damageAmount.ToString();
        if (isCrit)
        {
            textMesh.fontSize = 6f; 
            textMesh.color = Color.yellow;
            textColor = Color.yellow;
        }
        else
        {
            textMesh.fontSize = 4f;
            textMesh.color = Color.white;
            textColor = Color.white;
        }
        disappearTimer = 0.8f; 
    }

    // Cài đặt cho chữ "LEVEL UP!"
    public void SetupText(string text, Color color)
    {
        textMesh.text = text;
        textMesh.color = color;
        textColor = color;
        textMesh.fontSize = 5f;
        disappearTimer = 1.5f;
        moveYSpeed = 1.5f; 
    }

    private void Update()
    {
        // Bay từ từ lên trên
        transform.position += new Vector3(0, moveYSpeed) * Time.deltaTime;

        // Bắt đầu đếm ngược thời gian tồn tại
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            // Mờ dần rồi tự hủy (xóa rác)
            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;
            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}