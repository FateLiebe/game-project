using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Cài đặt Đạn")]
    public float speed = 8f;
    public float homingDuration = 0.5f;
    public float lifeTime = 4f;
    public bool destroyOnHit = true;

    private Transform target;
    private Vector2 currentDirection;
    private float timer = 0f;
    private UniversalHitbox hitbox;
    private bool hasHit = false; // cờ chống hit nhiều lần

    public void SetTarget(Transform customTarget)
    {
        target = customTarget;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
        hitbox = GetComponent<UniversalHitbox>();

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
            currentDirection = new Vector2(Mathf.Sign(transform.localScale.x), 0);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer <= homingDuration && target != null)
            currentDirection = (target.position - transform.position).normalized;

        transform.position += (Vector3)(currentDirection * speed * Time.deltaTime);

        float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (currentDirection.x < 0) transform.localScale = new Vector3(1, -1, 1);
        else transform.localScale = new Vector3(1, 1, 1);
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

        // Đánh dấu đã hit để chặn UniversalHitbox gọi lại lần 2
        hasHit = true;

        // Huỷ collider ngay lập tức để không trigger thêm
        // UniversalHitbox.OnTriggerEnter2D sẽ chạy cùng frame này và apply dame
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Destroy sau 1 frame để UniversalHitbox kịp chạy
        Destroy(gameObject, 0.05f);
    }
}