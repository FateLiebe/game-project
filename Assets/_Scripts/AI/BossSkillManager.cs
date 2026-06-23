using UnityEngine;

/// <summary>
/// Trạm điều phối kỹ năng (VFX) của Boss. 
/// Phân luồng hiệu ứng: Chỉnh sửa hướng lật ảnh (Facing), gắn bùa lợi (Aura/Shield) theo người Boss, hoặc gọi bão sét giáng thẳng xuống đầu Player.
/// </summary>
public class BossSkillManager : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    [Header("--- KHO VFX (7 KỸ NĂNG) ---")]
    public GameObject breathPrefab;
    public GameObject breathFirePrefab;
    public GameObject electroShockPrefab;
    public GameObject energyShieldPrefab;
    public GameObject energySmackPrefab;
    public GameObject fireBallPrefab;
    public GameObject slashHorizontalPrefab;

    [Header("--- VỊ TRÍ XUẤT CHIÊU ---")]
    public Transform mouthSpawnPoint;   
    public Transform centerSpawnPoint;  

    [Header("--- CÀI ĐẶT LẬT HÌNH ---")]
    public bool isVfxFacingLeftDefault = true; // Chuyển mặc định thành True do ảnh của bạn gốc quay trái

    private BaseEntity bossEntity;
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        bossEntity = GetComponent<BaseEntity>();
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Nơi "đúc" ra kỹ năng dựa trên Index từ Behavior Tree.
    /// Xử lý định vị động (Sét đánh theo vị trí Player hiện tại) hoặc gán sát thương duy trì (Breath DOT).
    /// </summary>
    public void SpawnVFXInstant(int skillIndex, int facingDir)
    {
        GameObject vfxToSpawn = null;
        Transform spawnPoint = centerSpawnPoint;
        bool isAura = false;
        bool isProjectile = false; 
        bool attachToBoss = false;
        bool spawnAtPlayer = false; // Đánh dấu kỹ năng có khả năng định vị mục tiêu (như sét đánh)

        switch (skillIndex)
        {
            case 0: vfxToSpawn = breathPrefab; spawnPoint = mouthSpawnPoint; break;
            case 1: vfxToSpawn = breathFirePrefab; spawnPoint = mouthSpawnPoint; break;
            case 2: vfxToSpawn = electroShockPrefab; spawnAtPlayer = true; break; // Skill 2 giáng thẳng sét xuống đầu Player
            case 3: vfxToSpawn = energyShieldPrefab; spawnPoint = centerSpawnPoint; isAura = true; attachToBoss = true; break;
            case 4: vfxToSpawn = energySmackPrefab; spawnPoint = centerSpawnPoint; isAura = true; attachToBoss = true; break;
            case 5: vfxToSpawn = fireBallPrefab; spawnPoint = mouthSpawnPoint; isProjectile = true; break;
            case 6: vfxToSpawn = slashHorizontalPrefab; spawnPoint = mouthSpawnPoint; break;
        }

        if (vfxToSpawn != null)
        {
            Vector3 finalSpawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

            // Đối với các kỹ năng định vị, tự động tìm và dò gốc tọa độ của Player để đánh xuống (Sét)
            if (spawnAtPlayer)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Collider2D pCol = player.GetComponent<Collider2D>();
                    if (pCol != null)
                    {
                        // Lấy vị trí ngay dưới chân Player
                        finalSpawnPos = new Vector3(player.transform.position.x, pCol.bounds.min.y + 0.5f, player.transform.position.z);
                    }
                    else
                    {
                        finalSpawnPos = player.transform.position;
                    }
                }
            }

            GameObject vfx;
            if (ObjectPoolManager.Instance != null) vfx = ObjectPoolManager.Instance.Get(vfxToSpawn, finalSpawnPos, Quaternion.identity);
            else vfx = Instantiate(vfxToSpawn, finalSpawnPos, Quaternion.identity);

            // Xử lý hướng lật ảnh (Facing) cho các đòn đánh tĩnh (Không phải luồng đạn và không phải sấm sét)
            if (!isAura && !isProjectile && !spawnAtPlayer)
            {
                Vector3 scale = vfx.transform.localScale;
                float finalFacing = isVfxFacingLeftDefault ? -facingDir : facingDir;
                scale.x = Mathf.Abs(scale.x) * finalFacing;
                vfx.transform.localScale = scale;
            }

            if (attachToBoss)
            {
                vfx.transform.SetParent(centerSpawnPoint);
                vfx.transform.localPosition = Vector3.zero;
            }

            if (isAura)
            {
                AuraEffect aura = vfx.GetComponent<AuraEffect>();
                if (aura != null)
                {
                    if (skillIndex == 3)
                    {
                        BossController boss = bossEntity as BossController;
                        if (boss != null)
                        {
                            boss.ActivateEnergyShield();
                            aura.SetupAura(bossEntity, 10f, 0, 0, 0);
                        }
                    }
                         
                    else if (skillIndex == 4) 
                    {
                        BossController boss = bossEntity as BossController;
                        if (boss != null)
                        {
                            boss.ActivateSmackBuff();
                            aura.SetupAura(bossEntity, 7f, 0, 0, 0);
                        }
                    }
                }
            }
            else
            {
                UniversalHitbox hitbox = vfx.GetComponent<UniversalHitbox>();
                if (hitbox != null) hitbox.owner = this.gameObject;

                // Truyền tham chiếu Owner cho hệ thống sát thương duy trì (Damage over Time) của Breath (skill 0 & 1)
                BreathDOT dot = vfx.GetComponent<BreathDOT>();
                if (dot != null) dot.owner = this.gameObject;

                // Truyền spawnPosition cho FireBall để tính dame theo khoảng cách
                BossHitboxData bossData = vfx.GetComponent<BossHitboxData>();
                if (bossData != null)
                {
                    bossData.spawnPosition = finalSpawnPos;
                }
            }
        }
    }
    #endregion
}