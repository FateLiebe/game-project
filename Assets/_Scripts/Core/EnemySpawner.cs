using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("--- CÀI ĐẶT QUÁI VẬT ---")]
    public GameObject[] enemyPrefabs; 
    
    [Tooltip("Thời gian chờ giữa mỗi lần đẻ quái TẠI TỪNG NODE (Giây)")]
    public float spawnDelay = 10f; 

    [Header("--- ĐIỂM SINH SẢN CỐ ĐỊNH ---")]
    public Transform[] spawnNodes; 

    [Header("--- TỐI ƯU KHOẢNG CÁCH ---")]
    public float maxDistanceFromPlayer = 80f;
    public float minDistanceFromPlayer = 5f;

    [Header("--- KẾT NỐI PLAYER ---")]
    public BaseEntity player;

    private GameObject[] enemiesAtNodes; 
    
    // Mảng lưu đồng hồ đếm ngược cho từng Node riêng biệt
    private float[] nodeRespawnTimers; 

    private void Awake()
    {
        int nodeCount = transform.childCount;
        if (nodeCount > 0)
        {
            spawnNodes = new Transform[nodeCount];
            enemiesAtNodes = new GameObject[nodeCount]; 
            nodeRespawnTimers = new float[nodeCount]; // Khởi tạo mảng đồng hồ
            
            for (int i = 0; i < nodeCount; i++)
            {
                spawnNodes[i] = transform.GetChild(i);
                nodeRespawnTimers[i] = spawnDelay; // Cài đặt đồng hồ ban đầu là 10s
            }
        }
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<BaseEntity>();
        }

        if (player != null) player.OnLevelChanged += HandlePlayerLevelUp;

        InitialSpawnAll();
        StartCoroutine(RespawnRoutine());
    }

    private void InitialSpawnAll()
    {
        if (spawnNodes == null || spawnNodes.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int startLevel = (player != null && player.currentLevel > 1) ? player.currentLevel + 3 : 1;

        // Tạo danh sách index node rồi shuffle trước khi spawn lần đầu
        List<int> nodeIndices = new List<int>();
        for (int i = 0; i < spawnNodes.Length; i++) nodeIndices.Add(i);
        ShuffleList(nodeIndices);

        foreach (int i in nodeIndices)
        {
            SpawnEnemyAtNode(i, startLevel);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // Thay vì chờ 1 cục 10s, giờ hệ thống sẽ "đi tuần" cứ 0.5s một lần để check đồng hồ
        WaitForSeconds checkInterval = new WaitForSeconds(0.5f);

        while (true)
        {
            yield return checkInterval;

            if (player == null) continue;

            Vector2 playerPos = player.transform.position;

            // Bước 1: Cập nhật đồng hồ cho từng node
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] == null)
                {
                    // Quái đã chết: trừ lùi đồng hồ của riêng node đó đi 0.5s
                    nodeRespawnTimers[i] -= 0.5f;
                }
                else
                {
                    // Quái vẫn sống: giữ đồng hồ luôn đầy ở mốc spawnDelay
                    nodeRespawnTimers[i] = spawnDelay;
                }
            }

            // Bước 2: Gom các node đã hết giờ và đủ điều kiện khoảng cách
            List<int> readyNodes = new List<int>();
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] == null && nodeRespawnTimers[i] <= 0f)
                {
                    float distanceToPlayer = Vector2.Distance(playerPos, (Vector2)spawnNodes[i].position);
                    if (distanceToPlayer >= minDistanceFromPlayer && distanceToPlayer <= maxDistanceFromPlayer)
                    {
                        readyNodes.Add(i);
                    }
                }
            }

            // Bước 3: Xáo trộn danh sách node sẵn sàng rồi spawn
            ShuffleList(readyNodes);

            int spawnLevel = (player.currentLevel == 1) ? 1 : player.currentLevel + 3;
            foreach (int nodeIndex in readyNodes)
            {
                SpawnEnemyAtNode(nodeIndex, spawnLevel);
            }
        }
    }

    // Hàm tiện ích: Fisher-Yates shuffle dùng chung
    private void ShuffleList(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private void SpawnEnemyAtNode(int nodeIndex, int level)
    {
        Transform selectedNode = spawnNodes[nodeIndex];

        // Chọn ngẫu nhiên prefab từ danh sách, bỏ qua nếu null
        int attempts = 0;
        GameObject randomPrefab = null;
        while (randomPrefab == null && attempts < enemyPrefabs.Length)
        {
            randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            attempts++;
        }
        if (randomPrefab == null) return;

        GameObject newEnemy = Instantiate(randomPrefab, selectedNode.position, Quaternion.identity, transform);
        
        BaseEntity enemyStats = newEnemy.GetComponent<BaseEntity>();
        if (enemyStats != null)
        {
            enemyStats.SetLevel(level); 
        }

        enemiesAtNodes[nodeIndex] = newEnemy;

        // Reset đồng hồ của node này sau khi spawn xong
        nodeRespawnTimers[nodeIndex] = spawnDelay;
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
                
                if (distanceToPlayer > maxDistanceFromPlayer)
                {
                    BaseEntity enemyStats = enemiesAtNodes[i].GetComponent<BaseEntity>();
                    if (enemyStats != null)
                    {
                        enemyStats.SetLevel(newLevel + 3);
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