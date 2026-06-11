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

    private void Start()
    {
        Destroy(gameObject, lifeTime); 
        hitbox = GetComponent<UniversalHitbox>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            currentDirection = (target.position - transform.position).normalized;
        }
        else
        {
            currentDirection = transform.right; 
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer <= homingDuration && target != null)
        {
            currentDirection = (target.position - transform.position).normalized;
        }

        transform.position += (Vector3)(currentDirection * speed * Time.deltaTime);

        // 1. CĂN GÓC BAY VỀ PHÍA PLAYER
        float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // 2. CHỐNG LẬT NGƯỢC (UPSIDE DOWN) KHI BAY SANG TRÁI
        if (currentDirection.x < 0)
        {
            // Lật trục Y để đầu đạn quay sang trái nhưng hình ảnh không bị chổng ngược
            transform.localScale = new Vector3(1, -1, 1);
        }
        else
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!destroyOnHit) return;

        Hurtbox targetHurtbox = collision.GetComponent<Hurtbox>();
        if (targetHurtbox != null && hitbox != null && hitbox.owner != null)
        {
            if (hitbox.owner.CompareTag("Enemy") && (collision.CompareTag("Player") || collision.transform.root.CompareTag("Player")))
            {
                Destroy(gameObject);
            }
            else if (hitbox.owner.CompareTag("Player") && (collision.CompareTag("Enemy") || collision.transform.root.CompareTag("Enemy")))
            {
                Destroy(gameObject);
            }
        }
    }
}