using UnityEngine;
using System.Collections;

/// <summary>
/// BreathDOT — Gắn lên prefab Breath (skill 0) và Breath Fire (skill 1).
/// Dùng BoxCollider2D có sẵn trên prefab để xác định vùng sát thương.
///
/// CÁCH DÙNG:
/// 1. Xóa (hoặc disable) UniversalHitbox trên prefab Breath/BreathFire
/// 2. Add Component → BreathDOT (BoxCollider2D đã có sẵn → không cần thêm)
/// 3. Điền totalDuration, damageScaleTotal, tickCount
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

    private void Start()
    {
        // Lấy mouthSpawnPoint từ boss
        if (followSpawnPoint && owner != null)
        {
            BossSkillManager bsm = owner.GetComponent<BossSkillManager>();
            if (bsm != null && bsm.mouthSpawnPoint != null)
                spawnTransform = bsm.mouthSpawnPoint;
        }

        StartCoroutine(DOTRoutine());
    }

    private void Update()
    {
        if (followSpawnPoint && spawnTransform != null)
            transform.position = spawnTransform.position;
    }

    private IEnumerator DOTRoutine()
    {
        if (tickCount <= 0) tickCount = 1;
        float interval      = totalDuration / tickCount;
        float damagePerTick = GetBaseDamage() * damageScaleTotal / tickCount;

        for (int i = 0; i < tickCount; i++)
        {
            yield return new WaitForSeconds(interval);
            DealDamageInBox(damagePerTick);
        }

        Destroy(gameObject);
    }

    private float GetBaseDamage()
    {
        if (owner == null) return 10f;
        BaseEntity entity = owner.GetComponent<BaseEntity>();
        return entity != null ? entity.Attack : 10f;
    }

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
}
