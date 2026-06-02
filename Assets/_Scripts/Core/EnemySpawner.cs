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
    public BaseEntity player;

    // CUỐN SỔ HỘ KHẨU: Mảng này có độ dài bằng số lượng Node.
    // Nó sẽ lưu lại xem Node nào đang chứa con quái nào.
    private GameObject[] enemiesAtNodes; 

    private void Awake()
    {
        int nodeCount = transform.childCount;
        if (nodeCount > 0)
        {
            spawnNodes = new Transform[nodeCount];
            enemiesAtNodes = new GameObject[nodeCount]; // Khởi tạo sổ hộ khẩu bằng đúng số lượng Node
            
            for (int i = 0; i < nodeCount; i++)
            {
                spawnNodes[i] = transform.GetChild(i);
            }
            Debug.Log($"<color=green>Spawner đã nạp {nodeCount} Node và tạo Sổ hộ khẩu thành công!</color>");
        }
    }

    private void Start()
    {
        if (player != null) player.OnLevelChanged += HandlePlayerLevelUp;
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
            // 1. Kiểm đếm số lượng quái đang còn sống trên toàn bản đồ
            int currentActiveCount = 0;
            for (int i = 0; i < enemiesAtNodes.Length; i++)
            {
                if (enemiesAtNodes[i] != null) 
                {
                    currentActiveCount++;
                }
            }

            // 2. Chỉ đẻ thêm nếu tổng số lượng chưa đạt Max
            if (currentActiveCount < maxEnemies)
            {
                TrySpawnEnemy();
            }
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void TrySpawnEnemy()
    {
        if (spawnNodes == null || spawnNodes.Length == 0 || enemyPrefabs.Length == 0 || player == null) return;

        List<int> availableNodeIndices = new List<int>();
        Vector2 playerPos = player.transform.position;

        // 3. QUÉT SỔ HỘ KHẨU TÌM NODE TRỐNG
        for (int i = 0; i < spawnNodes.Length; i++)
        {
            // Nếu enemiesAtNodes[i] == null nghĩa là Node này chưa từng đẻ, hoặc con quái của Node này đã bị chém chết (Destroy)
            if (enemiesAtNodes[i] == null)
            {
                float distanceToPlayer = Vector2.Distance(playerPos, (Vector2)spawnNodes[i].position);

                // Đảm bảo Node trống đó phải nằm trong vành đai cho phép của Player
                if (distanceToPlayer >= minDistanceFromPlayer && distanceToPlayer <= maxDistanceFromPlayer)
                {
                    availableNodeIndices.Add(i);
                }
            }
        }

        // 4. Bốc thăm 1 Node trong số các Node hợp lệ để đẻ
        if (availableNodeIndices.Count > 0)
        {
            int randomIndex = availableNodeIndices[Random.Range(0, availableNodeIndices.Count)];
            Transform selectedNode = spawnNodes[randomIndex];
            GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            
            GameObject newEnemy = Instantiate(randomPrefab, selectedNode.position, Quaternion.identity);
            
            // Ép cấp độ quái
            BaseEntity enemyStats = newEnemy.GetComponent<BaseEntity>();
            if (enemyStats != null)
            {
                if (player.currentLevel == 1) enemyStats.currentLevel = 1;
                else enemyStats.currentLevel = player.currentLevel + 3;
            }

            // 5. GHI DANH VÀO SỔ: Lưu con quái vừa đẻ vào đúng vị trí của Node đó
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