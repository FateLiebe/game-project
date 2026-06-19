using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TimeAnomalyManager : MonoBehaviour
{
    public static TimeAnomalyManager Instance;

    [Header("Time Stop Settings")]
    [SerializeField] private float slowFactor = 0.07f; // 93% Làm chậm
    [SerializeField] private float duration = 3f;      // Kéo dài 3 giây

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerPerfectDodge()
    {
        AudioManager.Instance?.PlayPerfectDodge(); // [AUDIO] Âm thanh Perfect Dodge
        TriggerTimeStop();
    }

    public void TriggerTimeStop()
    {
        StopAllCoroutines(); 
        AudioManager.Instance?.RevertTimeStop(); // Reset pitch nếu còn dư từ lần trước
        StartCoroutine(TimeStopRoutine());
    }

    private IEnumerator TimeStopRoutine()
    {
        // [FIX UNITY 6]: Tìm kiếm an toàn
        BaseEntity[] allEntities = Object.FindObjectsByType<BaseEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Rigidbody2D[] allRbs = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Animator[] allAnims = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        List<BaseEntity> affectedEntities = new List<BaseEntity>();
        List<Rigidbody2D> affectedRbs = new List<Rigidbody2D>();
        List<Animator> affectedAnims = new List<Animator>();

        // 1. BÓP ĐỒNG HỒ LOGIC (Cho Quái vật đi chậm, đếm giờ chậm)
        foreach (var entity in allEntities)
        {
            if (!entity.CompareTag("Player"))
            {
                affectedEntities.Add(entity);
                entity.timeMultiplier = slowFactor;
            }
        }

        // 2. BÓP VẬT LÝ TỰ DO (Cho Đạn, Mũi tên bay chậm lại)
        foreach (var rb in allRbs)
        {
            if (!rb.CompareTag("Player"))
            {
                affectedRbs.Add(rb);
                rb.gravityScale *= slowFactor;  
                
                // FIX LỖI TÊN LỬA: Kiểm tra BaseEntity thay vì EnemyController để bao quát mọi loại quái
                if (rb.GetComponent<BaseEntity>() == null)
                    rb.linearVelocity *= slowFactor; 
            }
        }

        // 3. BÓP KHUNG HÌNH (Animation)
        foreach (var anim in allAnims)
        {
            if (!anim.CompareTag("Player"))
            {
                affectedAnims.Add(anim);
                anim.speed = slowFactor; 
            }
        }

        // TẬN HƯỞNG 3 GIÂY
        AudioManager.Instance?.ApplyTimeStop(slowFactor); // [AUDIO] Bóp pitch
        yield return new WaitForSeconds(duration);

        // TRẢ LẠI THỜI GIAN NHƯ CŨ
        AudioManager.Instance?.RevertTimeStop(); // [AUDIO] Trả lại pitch
        foreach (var entity in affectedEntities)
        {
            if (entity != null) entity.timeMultiplier = 1f;
        }

        foreach (var rb in affectedRbs)
        {
            if (rb != null)
            {
                rb.gravityScale /= slowFactor;
                // FIX LỖI TÊN LỬA: Ngăn chặn việc chia 0.01 gây sốc vận tốc cho quái
                if (rb.GetComponent<BaseEntity>() == null)
                    rb.linearVelocity /= slowFactor;
            }
        }

        foreach (var anim in affectedAnims)
        {
            if (anim != null) anim.speed = 1f;
        }
    }
}