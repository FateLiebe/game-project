using UnityEngine;

public class VFX_AutoDestroy : MonoBehaviour
{
    private void OnEnable()
    {
        Animator anim = GetComponent<Animator>();
        float delay = 1f;
        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            delay = stateInfo.length > 0 ? stateInfo.length : 1f;
        }
        StartCoroutine(AutoDestroyRoutine(delay));
    }

    private System.Collections.IEnumerator AutoDestroyRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}