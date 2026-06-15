using UnityEngine;

public class BossSkillManager : MonoBehaviour
{
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

    private void Awake()
    {
        bossEntity = GetComponent<BaseEntity>();
    }

    public void SpawnVFXInstant(int skillIndex, int facingDir)
    {
        GameObject vfxToSpawn = null;
        Transform spawnPoint = centerSpawnPoint;
        bool isAura = false;
        bool isProjectile = false; 
        bool attachToBoss = false;
        bool spawnAtPlayer = false; // [MỚI]: Cơ chế định vị mục tiêu

        switch (skillIndex)
        {
            case 0: vfxToSpawn = breathPrefab; spawnPoint = mouthSpawnPoint; break;
            case 1: vfxToSpawn = breathFirePrefab; spawnPoint = mouthSpawnPoint; break;
            case 2: vfxToSpawn = electroShockPrefab; spawnAtPlayer = true; break; // [ĐÃ SỬA]: Sét đánh thẳng vào Player
            case 3: vfxToSpawn = energyShieldPrefab; spawnPoint = centerSpawnPoint; isAura = true; attachToBoss = true; break;
            case 4: vfxToSpawn = energySmackPrefab; spawnPoint = centerSpawnPoint; isAura = true; attachToBoss = true; break;
            case 5: vfxToSpawn = fireBallPrefab; spawnPoint = mouthSpawnPoint; isProjectile = true; break;
            case 6: vfxToSpawn = slashHorizontalPrefab; spawnPoint = mouthSpawnPoint; break;
        }

        if (vfxToSpawn != null)
        {
            Vector3 finalSpawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

            // [XỬ LÝ ĐẶC BIỆT]: NẾU LÀ SÉT, TÌM CHÂN PLAYER ĐỂ ĐÁNH XUỐNG
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

            GameObject vfx = Instantiate(vfxToSpawn, finalSpawnPos, Quaternion.identity);

            // XỬ LÝ LẬT ẢNH CHO ĐÒN ĐÁNH TĨNH VÀ KHÔNG PHẢI SÉT
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
                        aura.SetupAura(bossEntity, 15f, bossEntity.Attack * 0.5f, bossEntity.Defense * 0.5f, 0);
                }
            }
            else
            {
                UniversalHitbox hitbox = vfx.GetComponent<UniversalHitbox>();
                if (hitbox != null) hitbox.owner = this.gameObject;

                // Truyền spawnPosition cho FireBall để tính dame theo khoảng cách
                BossHitboxData bossData = vfx.GetComponent<BossHitboxData>();
                if (bossData != null)
                {
                    bossData.spawnPosition = finalSpawnPos;
                }
            }
        }
    }
}