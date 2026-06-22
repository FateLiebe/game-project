using UnityEngine;
using System.Collections;

/// <summary>
/// Quản lý kỹ năng Khạc Lửa (Breath DOT - Damage Over Time) của Boss.
/// Không dùng va chạm vật lý thông thường mà dùng hàm quét OverlapBox liên tục theo chu kỳ (Tick) để rỉa máu Player liên tục khi đứng trong luồng lửa.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BreathDOT : MonoBehaviour
{
    [Header("DOT Settings")]
    [Tooltip("Tổng thời gian hiệu ứng tồn tại (giây)")]
    public float totalDuration = 2f;

    [Tooltip("Tổng scale sát thương (bằng scale cũ, VD: 1.0 = 100% ATK boss)")]
    public float damageScaleTotal = 1.0f;

    [Tooltip("Số lần gây sát thương trong totalDuration")]
    public int tickCount = 4;

    [Header("Follow Spawn Point")]
    [Tooltip("Nếu true, VFX bám theo mouthSpawnPoint của boss")]
    public bool followSpawnPoint = true;

    // Được BossSkillManager gán
    [HideInInspector] public GameObject owner;

    private BoxCollider2D box;
    private Transform spawnTransform;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        // Tắt isTrigger để tránh UniversalHitbox cũ (nếu chưa xóa) kích hoạt
        // BreathDOT tự kiểm tra vùng bằng OverlapBox
        box.isTrigger = true;
        box.enabled   = false; // Tắt collider vật lý — BreathDOT tự check thủ công
    }

    /// <summary>
    /// Kích hoạt chuỗi sát thương theo thời gian.
    /// Gắn vị trí của đạn/tia sáng bám theo miệng Boss nếu được thiết lập.
    /// </summary>
    private void OnEnable()
    {
        StartCoroutine(DOTRoutine());
    }

    private void OnDisable()
    {
        owner = null;
        spawnTransform = null;
    }

    /// <summary>
    /// Đồng bộ hóa vị trí của VFX bám theo miệng Boss trong quá trình duy trì.
    /// </summary>
    private void Update()
    {
        if (followSpawnPoint && spawnTransform != null)
            transform.position = spawnTransform.position;
    }

    /// <summary>
    /// Vòng lặp chia nhỏ sát thương thành nhiều lần (Ticks) dựa trên tổng thời gian và tổng tỷ lệ sát thương.
    /// Hỗ trợ chịu ảnh hưởng bởi ngưng đọng thời gian (timeMultiplier).
    /// </summary>
    private IEnumerator DOTRoutine()
    {
        yield return null; // Chờ 1 frame để BossSkillManager gán owner
        
        if (followSpawnPoint && owner != null)
        {
            BossSkillManager bsm = owner.GetComponent<BossSkillManager>();
            if (bsm != null && bsm.mouthSpawnPoint != null)
                spawnTransform = bsm.mouthSpawnPoint;
        }

        if (tickCount <= 0) tickCount = 1;
        float interval      = totalDuration / tickCount;
        float damagePerTick = GetBaseDamage() * damageScaleTotal / tickCount;

        for (int i = 0; i < tickCount; i++)
        {
            yield return new WaitForSeconds(interval);
            DealDamageInBox(damagePerTick);
        }

        ReturnOrDestroy();
    }

    private float GetBaseDamage()
    {
        if (owner == null) return 10f;
        BaseEntity entity = owner.GetComponent<BaseEntity>();
        return entity != null ? entity.Attack : 10f;
    }

    /// <summary>
    /// Quét BoxCollider để tìm tất cả các mục tiêu nằm trong luồng sát thương và tiến hành trừ máu.
    /// </summary>
    /// <param name="damage">Sát thương mỗi lần quét (Tick Damage)</param>
    private void DealDamageInBox(float damage)
    {
        // Lấy thông số box trong world space
        Vector2 center = (Vector2)transform.position + box.offset;
        Vector2 size   = box.size * (Vector2)transform.lossyScale; // tính cả scale

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, transform.eulerAngles.z);

        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;

            Hurtbox hurtbox = col.GetComponent<Hurtbox>()
                           ?? col.GetComponentInChildren<Hurtbox>();
            if (hurtbox == null) continue;

            float pushDir = col.transform.position.x < transform.position.x ? -1f : 1f;
            DamageInfo info = new DamageInfo
            {
                damage         = damage,
                knockbackForce = new Vector2(pushDir * 1f, 0.5f),
                attacker       = owner,
                isCritical     = false
            };
            hurtbox.TakeDamage(info);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (box == null) box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(box.offset, box.size);
    }

    /// <summary>
    /// Trả Prefab về lại Object Pool để tái sử dụng.
    /// </summary>
    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}
