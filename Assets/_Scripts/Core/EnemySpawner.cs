using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("--- CÀI ĐẶT QUÁI VẬT ---")]
    public GameObject[] enemyPrefabs; 
    public int maxEnemies = 20;
    
    [Tooltip("Thời gian chờ giữa mỗi lần hệ thống kiểm tra và đẻ quái (Giây)")]
    public float spawnDelay = 2f; 

    [Header("--- ĐIỂM SINH SẢN CỐ ĐỊNH ---")]
    [Tooltip("Danh sách các điểm đẻ quái (Tự động nạp)")]
    public Transform[] spawnNodes; 

    [Header("--- TỐI ƯU KHOẢNG CÁCH ---")]
    public float maxDistanceFromPlayer = 25f;
    public float minDistanceFromPlayer = 10f;

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
        // [ĐÃ SỬA]: Tự động tìm Player xuyên Scene bằng Tag
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

        StartCoroutine(SpawnRoutine());
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        TrySpawnEnemy();
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            int currentActiveCount = 0;
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] != null) 
                {
                    currentActiveCount++;
                }
            }

            if (currentActiveCount < maxEnemies)
            {
                TrySpawnEnemy();
            }
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void TrySpawnEnemy()
    {
        // Thêm kiểm tra enemyPrefabs để tránh lỗi Null
        if (spawnNodes == null || spawnNodes.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0 || player == null) return;

        List<int> availableNodeIndices = new List<int>();
        Vector2 playerPos = player.transform.position;

        for (int i = 0; i < spawnNodes.Length; i++)
        {
            if (enemiesAtNodes[i] == null)
            {
                float distanceToPlayer = Vector2.Distance(playerPos, (Vector2)spawnNodes[i].position);

                if (distanceToPlayer >= minDistanceFromPlayer && distanceToPlayer <= maxDistanceFromPlayer)
                {
                    availableNodeIndices.Add(i);
                }
            }
        }

        if (availableNodeIndices.Count > 0)
        {
            int randomIndex = availableNodeIndices[Random.Range(0, availableNodeIndices.Count)];
            Transform selectedNode = spawnNodes[randomIndex];
            
            GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (randomPrefab == null) return;

            GameObject newEnemy = Instantiate(randomPrefab, selectedNode.position, Quaternion.identity);
            
            BaseEntity enemyStats = newEnemy.GetComponent<BaseEntity>();
            if (enemyStats != null)
            {
                if (player.currentLevel == 1) enemyStats.currentLevel = 1;
                else enemyStats.currentLevel = player.currentLevel + 3;
            }

            enemiesAtNodes[randomIndex] = newEnemy;
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