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

    [Header("--- ELITE LIMIT ---")]
    [Tooltip("Tỷ lệ tối đa enemy Elite trên toàn map (0.3 = 30%)")]
    [Range(0f, 1f)]
    public float eliteRatioLimit = 0.3f;

    private GameObject[] enemiesAtNodes; 
    private float[] nodeRespawnTimers; 

    private void Awake()
    {
        int nodeCount = transform.childCount;
        if (nodeCount > 0)
        {
            spawnNodes = new Transform[nodeCount];
            enemiesAtNodes = new GameObject[nodeCount]; 
            nodeRespawnTimers = new float[nodeCount]; 
            
            for (int i = 0; i < nodeCount; i++)
            {
                spawnNodes[i] = transform.GetChild(i);
                nodeRespawnTimers[i] = spawnDelay; 
            }
        }
    }

    private IEnumerator Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<BaseEntity>();
        }

        if (player != null) player.OnLevelChanged += HandlePlayerLevelUp;

        // Chờ 0.2s để GameLoader kịp nạp file Save vào Player
        yield return new WaitForSeconds(0.2f);

        InitialSpawnAll();
        StartCoroutine(RespawnRoutine());
    }

    private void InitialSpawnAll()
    {
        if (spawnNodes == null || spawnNodes.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int startLevel = (player != null && player.currentLevel > 1) ? player.currentLevel + 3 : 1;

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
        WaitForSeconds checkInterval = new WaitForSeconds(0.5f);

        while (true)
        {
            yield return checkInterval;
            if (player == null) continue;

            Vector2 playerPos = player.transform.position;

            // Cập nhật timer
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] == null)
                    nodeRespawnTimers[i] -= 0.5f;
                else
                    nodeRespawnTimers[i] = spawnDelay;
            }

            // Tìm node sẵn sàng
            List<int> readyNodes = new List<int>();
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] == null && nodeRespawnTimers[i] <= 0f)
                {
                    float dist = Vector2.Distance(playerPos, (Vector2)spawnNodes[i].position);
                    if (dist >= minDistanceFromPlayer && dist <= maxDistanceFromPlayer)
                        readyNodes.Add(i);
                }
            }

            ShuffleList(readyNodes);

            int spawnLevel = (player.currentLevel == 1) ? 1 : player.currentLevel + 3;
            foreach (int nodeIndex in readyNodes)
                SpawnEnemyAtNode(nodeIndex, spawnLevel);
        }
    }

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

    // ============================================================
    // ĐẾM SỐ ELITE ĐANG SỐNG
    // ============================================================
    private int CountAliveElites()
    {
        int count = 0;
        foreach (var obj in enemiesAtNodes)
        {
            if (obj == null) continue;
            EnemyController ec = obj.GetComponent<EnemyController>();
            if (ec != null && ec.rank == EnemyBase.EnemyRank.Elite)
                count++;
        }
        return count;
    }

    private int CountAliveEnemies()
    {
        int count = 0;
        foreach (var obj in enemiesAtNodes)
            if (obj != null) count++;
        return count;
    }

    // ============================================================
    // SPAWN
    // ============================================================
    private void SpawnEnemyAtNode(int nodeIndex, int level)
    {
        Transform selectedNode = spawnNodes[nodeIndex];

        // --- Kiểm tra giới hạn Elite ---
        int totalAlive  = CountAliveEnemies();
        int eliteAlive  = CountAliveElites();
        // Nếu tỷ lệ elite đã đủ 30%, ép spawn thường
        bool forceNormal = totalAlive > 0 && (float)eliteAlive / (totalAlive + 1) >= eliteRatioLimit;

        int attempts = 0;
        GameObject randomPrefab = null;

        while (attempts < enemyPrefabs.Length * 3)
        {
            GameObject candidate = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (candidate == null) { attempts++; continue; }

            if (forceNormal)
            {
                // Chỉ chọn nếu là quái thường (không có EnemyRank.Elite trên prefab)
                EnemyController ec = candidate.GetComponent<EnemyController>();
                if (ec != null && ec.rank == EnemyBase.EnemyRank.Elite)
                {
                    attempts++;
                    continue; // Bỏ qua elite
                }
            }

            randomPrefab = candidate;
            break;
        }

        // Fallback: nếu không tìm được prefab phù hợp, lấy bất kỳ
        if (randomPrefab == null)
            randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (randomPrefab == null) return;

        GameObject newEnemy = Instantiate(randomPrefab, selectedNode.position, Quaternion.identity, transform);
        
        BaseEntity enemyStats = newEnemy.GetComponent<BaseEntity>();
        if (enemyStats != null) enemyStats.SetLevel(level);

        enemiesAtNodes[nodeIndex] = newEnemy;
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
                float dist = Vector2.Distance(playerPos, enemiesAtNodes[i].transform.position);
                if (dist > maxDistanceFromPlayer)
                {
                    BaseEntity stats = enemiesAtNodes[i].GetComponent<BaseEntity>();
                    if (stats != null) stats.SetLevel(newLevel + 3);
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
                if (node != null) Gizmos.DrawWireSphere(node.position, 0.5f);
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