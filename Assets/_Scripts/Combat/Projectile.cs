using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Cài đặt Đạn")]
    public float speed = 8f;
    public float homingDuration = 0.5f;
    public float lifeTime = 4f;
    public bool destroyOnHit = true;

    [Tooltip("Nếu ảnh gốc quay sang TRÁI thay vì phải, nhập 180. Mặc định 0.")]
    public float rotationOffset = 0f;

    private Transform target;
    private Vector2 currentDirection;
    private float timer = 0f;
    private UniversalHitbox hitbox;
    private bool hasHit = false;

    public void SetTarget(Transform customTarget)
    {
        target = customTarget;
    }

    private void OnEnable()
    {
        timer = 0f;
        hasHit = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
        hitbox = GetComponent<UniversalHitbox>();
        
        StartCoroutine(InitAndLifeTimeRoutine());
    }

    private void OnDisable()
    {
        target = null;
    }

    private System.Collections.IEnumerator InitAndLifeTimeRoutine()
    {
        yield return null; // Chờ 1 frame để SetTarget() kịp chạy nếu được gọi ngay sau khi Get() từ pool

        if (target == null)
        {
            bool isOwnedByPlayer = false;
            if (hitbox != null && hitbox.owner != null)
                isOwnedByPlayer = hitbox.owner.CompareTag("Player") || hitbox.owner.transform.root.CompareTag("Player");

            if (!isOwnedByPlayer)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) target = playerObj.transform;
            }
        }

        if (target != null)
            currentDirection = (target.position - transform.position).normalized;
        else
        {
            // Không có target: bay theo hướng rotation hiện tại của prefab
            currentDirection = transform.right;
        }

        yield return new WaitForSeconds(lifeTime);
        ReturnOrDestroy();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer <= homingDuration && target != null)
            currentDirection = (target.position - transform.position).normalized;

        transform.position += (Vector3)(currentDirection * speed * Time.deltaTime);

        // Rotation hoàn toàn xử lý hướng, không đụng scale
        float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!destroyOnHit || hasHit) return;
        if (hitbox == null || hitbox.owner == null) return;

        Hurtbox targetHurtbox = collision.GetComponent<Hurtbox>();
        if (targetHurtbox == null) return;

        bool ownerIsPlayer = hitbox.owner.CompareTag("Player") || hitbox.owner.transform.root.CompareTag("Player");
        bool victimIsPlayer = collision.CompareTag("Player") || collision.transform.root.CompareTag("Player");

        bool shouldHit = (!ownerIsPlayer && victimIsPlayer) || (ownerIsPlayer && !victimIsPlayer);
        if (!shouldHit) return;

        hasHit = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(HitDelayRoutine());
    }

    private System.Collections.IEnumerator HitDelayRoutine()
    {
        yield return new WaitForSeconds(0.05f);
        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        PooledObject po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}