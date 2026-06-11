using UnityEngine;

public class VFX_AutoDestroy : MonoBehaviour
{
    private void Start()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            // Tự động đọc xem Animation dài bao nhiêu giây và hẹn giờ tự hủy
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            Destroy(gameObject, stateInfo.length);
        }
        else
        {
            Destroy(gameObject, 1f); 
        }
    }
}