using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("--- CÀI ĐẶT QUÁI VẬT ---")]
    public GameObject[] enemyPrefabs; 
    
    [Tooltip("Thời gian chờ giữa mỗi lần hệ thống kiểm tra và đẻ quái (Giây)")]
    public float spawnDelay = 10f; 

    [Header("--- ĐIỂM SINH SẢN CỐ ĐỊNH ---")]
    [Tooltip("Danh sách các điểm đẻ quái (Tự động nạp)")]
    public Transform[] spawnNodes; 

    [Header("--- TỐI ƯU KHOẢNG CÁCH ---")]
    public float maxDistanceFromPlayer = 30f;
    public float minDistanceFromPlayer = 5f;

    [Header("--- KẾT NỐI PLAYER ---")]
    [Tooltip("Hệ thống sẽ tự động tìm Player xuyên Scene")]
    public BaseEntity player;

    private GameObject[] enemiesAtNodes; 

    private void Awake()
    {
        int nodeCount = transform.childCount;
        if (nodeCount > 0)
        {
            spawnNodes = new Transform[nodeCount];
            enemiesAtNodes = new GameObject[nodeCount]; 
            
            for (int i = 0; i < nodeCount; i++)
            {
                spawnNodes[i] = transform.GetChild(i);
            }
        }
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) 
            {
                player = p.GetComponent<BaseEntity>();
            }
        }

        if (player != null) 
        {
            player.OnLevelChanged += HandlePlayerLevelUp;
        }
        else 
        {
            Debug.LogError("<color=red>EnemySpawner KHÔNG TÌM THẤY PLAYER! Hãy đảm bảo Object Player của bạn được gắn Tag là 'Player'.</color>");
        }

        // 1. Vừa vào Map là đẻ quái luôn
        InitialSpawnAll();

        // 2. Chạy vòng lặp kiểm tra hồi sinh quái chết
        StartCoroutine(RespawnRoutine());
    }

    private void InitialSpawnAll()
    {
        if (spawnNodes == null || spawnNodes.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        // Nếu mới vào game (Player lv 1) thì quái lv 1. Nếu Player đã sang map khác và cấp cao thì lấy Lv + 3
        int startLevel = (player != null && player.currentLevel > 1) ? player.currentLevel + 3 : 1;

        for (int i = 0; i < spawnNodes.Length; i++)
        {
            SpawnEnemyAtNode(i, startLevel);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        while (true)
        {

            yield return new WaitForSeconds(spawnDelay);

            if (player == null) continue;

            Vector2 playerPos = player.transform.position;

            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                // Nếu quái ở Node này đã chết
                if (enemiesAtNodes[i] == null)
                {

                    yield return new WaitForSeconds(spawnDelay);

                    float distanceToPlayer = Vector2.Distance(playerPos, (Vector2)spawnNodes[i].position);

                    // Chỉ hồi sinh quái nếu Player nằm trong phạm vi Spawn
                    if (distanceToPlayer >= minDistanceFromPlayer && distanceToPlayer <= maxDistanceFromPlayer)
                    {
                        int spawnLevel = (player.currentLevel == 1) ? 1 : player.currentLevel + 3;
                        SpawnEnemyAtNode(i, spawnLevel);
                    }
                }
            }
        }
    }

    private void SpawnEnemyAtNode(int nodeIndex, int level)
    {
        Transform selectedNode = spawnNodes[nodeIndex];
        
        GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (randomPrefab == null) return;

        GameObject newEnemy = Instantiate(randomPrefab, selectedNode.position, Quaternion.identity, transform);
        
        BaseEntity enemyStats = newEnemy.GetComponent<BaseEntity>();
        if (enemyStats != null)
        {
            enemyStats.currentLevel = level;
            // Bơm đầy máu theo level
            enemyStats.currentHealth = enemyStats.MaxHealth; 
        }

        enemiesAtNodes[nodeIndex] = newEnemy;
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        if (player == null || enemiesAtNodes == null) return;

        Vector2 playerPos = player.transform.position;

        for (int i = 0; i < enemiesAtNodes.Length; i++)
        {
            if (enemiesAtNodes[i] != null)
            {
                float distanceToPlayer = Vector2.Distance(playerPos, enemiesAtNodes[i].transform.position);
                
                // Nếu quái nằm NGOÀI phạm vi spawn (quá gần < min HOẶC quá xa > max) thì cập nhật cấp
                if (distanceToPlayer > maxDistanceFromPlayer)
                {
                    BaseEntity enemyStats = enemiesAtNodes[i].GetComponent<BaseEntity>();
                    if (enemyStats != null)
                    {
                        enemyStats.currentLevel = newLevel + 3;
                        enemyStats.Heal(99999f); // Bơm máu để UI cập nhật
                    }
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        if (player != null) player.OnLevelChanged -= HandlePlayerLevelUp;
    }
    
    private void OnDrawGizmos()
    {
        if (spawnNodes != null)
        {
            Gizmos.color = Color.red;
            foreach (Transform node in spawnNodes)
            {
                if (node != null) Gizmos.DrawWireSphere(node.position, 0.5f);
            }
        }

        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.transform.position, maxDistanceFromPlayer);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(player.transform.position, minDistanceFromPlayer);
        }
    }
}