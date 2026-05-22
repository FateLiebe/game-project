using UnityEngine;

public class BackgroundFollowCam : MonoBehaviour
{
    private Transform cam;

    private void Awake()
    {
        cam = Camera.main.transform;
        // Snap ngay lập tức về vị trí camera khi game bắt đầu
        transform.position = new Vector3(
            cam.position.x,
            cam.position.y,
            transform.position.z
        );
    }

    private void LateUpdate()
    {
        transform.position = new Vector3(
            cam.position.x,
            cam.position.y,
            transform.position.z
        );
    }
}