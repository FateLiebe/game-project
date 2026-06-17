using UnityEngine;
using System.Collections;

/// <summary>
/// AudioManager — Singleton quản lý TOÀN BỘ âm thanh game.
/// Gắn vào GameObject "AudioManager" trong scene Core_Gameplay.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ============================================================
    // AUDIO SOURCES (tự động tạo)
    // ============================================================
    private AudioSource srcAmbient;
    private AudioSource srcSFX;      // SFX player (không bị Time Stop)
    private AudioSource srcWorld;    // SFX world / VFX (bị Time Stop)
    private AudioSource srcUI;
    private AudioSource srcRun;      // Riêng cho run loop
    private AudioSource bossWingSource;

    // ============================================================
    // ÂM THANH MÔI TRƯỜNG
    // ============================================================
    [Header("🌿 Âm Thanh Môi Trường")]
    public AudioClip[] ambientClips;
    [Range(0f, 1f)] public float ambientVolume = 0.6f;

    // ============================================================
    // ÂM THANH PLAYER
    // ============================================================
    [Header("🗡️ Âm Thanh Player")]
    public AudioClip[] attackCombo;
    [Range(0f, 1f)] public float attackVolume = 1f;

    public AudioClip dashClip;
    [Range(0f, 1f)] public float dashVolume = 1f;

    public AudioClip fallLoopClip;
    [Range(0f, 1f)] public float fallLoopVolume = 0.8f;

    public AudioClip jumpClip;
    [Range(0f, 1f)] public float jumpVolume = 1f;

    public AudioClip landClip;
    [Range(0f, 1f)] public float landVolume = 1f;

    public AudioClip runClip;
    [Range(0f, 1f)] public float runVolume = 0.6f;

    // ============================================================
    // ÂM THANH INVENTORY
    // ============================================================
    [Header("🎒 Âm Thanh Inventory")]
    public AudioClip inventoryOpenClip;
    [Range(0f, 1f)] public float inventoryOpenVolume = 1f;

    public AudioClip inventoryCloseClip;
    [Range(0f, 1f)] public float inventoryCloseVolume = 1f;

    public AudioClip equipClip;
    [Range(0f, 1f)] public float equipVolume = 1f;

    public AudioClip unequipClip;
    [Range(0f, 1f)] public float unequipVolume = 1f;

    // ============================================================
    // ÂM THANH VFX / BOSS SKILLS
    // ============================================================
    [Header("✨ Âm Thanh VFX / Boss Skills (theo thứ tự skill 0-6)")]
    public AudioClip[] vfxClips;
    [Range(0f, 1f)] public float vfxVolume = 1f;

    // ============================================================
    // ÂM THANH BOSS
    // ============================================================
    [Header("👾 Âm Thanh Boss")]
    public AudioClip bossWingFlapClip;
    [Range(0f, 1f)] public float bossWingVolume = 0.5f;

    // ============================================================
    // ÂM THANH HỆ THỐNG
    // ============================================================
    [Header("⚙️ Âm Thanh Hệ Thống")]
    public AudioClip perfectDodgeClip;
    [Range(0f, 1f)] public float perfectDodgeVolume = 1f;

    public AudioClip levelUpClip;
    [Range(0f, 1f)] public float levelUpVolume = 1f;

    public AudioClip gameOverClip;
    [Range(0f, 1f)] public float gameOverVolume = 1f;

    public AudioClip uiClickClip;
    [Range(0f, 1f)] public float uiClickVolume = 0.8f;

    // ============================================================
    // CÀI ĐẶT MASTER (lưu/load từ save)
    // ============================================================
    [Header("🔊 Master Volume")]
    [Range(1f, 10f)] public float masterSliderValue = 10f; // 1-10 như UI thấy
    public bool isMuted = false;

    // ============================================================
    // TRẠNG THÁI NỘI BỘ
    // ============================================================
    private Coroutine ambientCycleCoroutine;
    private Coroutine fallLoopCoroutine;
    private int[] lastThreeAmbient = new int[] { -1, -1, -1 };
    private int ambientHistoryIndex = 0;

    // Time Stop — pitch của srcWorld và bossWingSource bị bóp
    private float normalPitch = 1f;
    private bool isTimeStopActive = false;

    // ============================================================
    // KHỞI TẠO
    // ============================================================
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateAudioSources();
        }
        else Destroy(gameObject);
    }

    private void CreateAudioSources()
    {
        srcAmbient = AddSrc(loop: false);
        srcSFX     = AddSrc(loop: false);   // Player SFX — không bị Time Stop
        srcWorld   = AddSrc(loop: false);   // World/VFX SFX — bị Time Stop
        srcUI      = AddSrc(loop: false);
        srcRun     = AddSrc(loop: true);    // Run loop riêng
        bossWingSource = AddSrc(loop: true);
    }

    private AudioSource AddSrc(bool loop)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        return src;
    }

    // ============================================================
    // VOLUME CONTROL
    // ============================================================
    public float MasterVolume => isMuted ? 0f : Mathf.Clamp(masterSliderValue, 1f, 10f) / 10f;

    /// <summary>Gọi từ Slider (1-10) trên PauseScreen</summary>
    public void SetVolume(float sliderValue)
    {
        masterSliderValue = sliderValue;
        ApplyVolumeToContinuousSources();
    }

    /// <summary>Gọi từ Toggle bật/tắt trên PauseScreen</summary>
    public void SetMute(bool muted)
    {
        isMuted = muted;
        ApplyVolumeToContinuousSources();
    }

    /// Cập nhật volume cho các source đang loop liên tục
    private void ApplyVolumeToContinuousSources()
    {
        float m = MasterVolume;
        srcAmbient.volume     = m * ambientVolume;
        srcRun.volume         = m * runVolume;
        bossWingSource.volume = m * bossWingVolume;
    }

    // ============================================================
    // ÂM THANH MÔI TRƯỜNG
    // ============================================================
    public void RestartAmbientCycle()
    {
        if (ambientCycleCoroutine != null) StopCoroutine(ambientCycleCoroutine);
        srcAmbient.Stop();
        // Reset lịch sử để tránh phát lại clip cũ
        lastThreeAmbient = new int[] { -1, -1, -1 };
        ambientHistoryIndex = 0;
        ambientCycleCoroutine = StartCoroutine(AmbientCycleRoutine());
    }

    public void StopAmbient()
    {
        if (ambientCycleCoroutine != null) StopCoroutine(ambientCycleCoroutine);
        srcAmbient.Stop();
    }

    private IEnumerator AmbientCycleRoutine()
    {
        while (true)
        {
            // Nghỉ 7~9s — dùng WaitForSecondsRealtime để không bị ảnh hưởng timeScale
            yield return new WaitForSecondsRealtime(Random.Range(7f, 9f));

            if (ambientClips == null || ambientClips.Length == 0) continue;
            int chosen = PickAmbientClip();
            AudioClip clip = ambientClips[chosen];
            if (clip == null) continue;

            srcAmbient.volume = MasterVolume * ambientVolume;
            srcAmbient.pitch  = 1f; // Ambient không bị Time Stop
            srcAmbient.clip   = clip;
            srcAmbient.Play();

            // Chờ hết clip (real time)
            yield return new WaitForSecondsRealtime(clip.length);

            // Nghỉ lần 2
            yield return new WaitForSecondsRealtime(Random.Range(7f, 9f));
        }
    }

    private int PickAmbientClip()
    {
        if (ambientClips.Length <= 3) return Random.Range(0, ambientClips.Length);
        int chosen; int safe = 0;
        do { chosen = Random.Range(0, ambientClips.Length); safe++; }
        while (System.Array.IndexOf(lastThreeAmbient, chosen) >= 0 && safe < 20);
        lastThreeAmbient[ambientHistoryIndex % 3] = chosen;
        ambientHistoryIndex++;
        return chosen;
    }

    // ============================================================
    // ÂM THANH PLAYER
    // ============================================================
    public void PlayAttack(int comboStep)
    {
        int idx = Mathf.Clamp(comboStep - 1, 0, (attackCombo?.Length ?? 1) - 1);
        if (attackCombo != null && attackCombo.Length > idx)
            srcSFX.PlayOneShot(attackCombo[idx], MasterVolume * attackVolume);
    }

    public void PlayDash()  => srcSFX.PlayOneShot(dashClip,  MasterVolume * dashVolume);
    public void PlayJump()  => srcSFX.PlayOneShot(jumpClip,  MasterVolume * jumpVolume);
    public void PlayLand()  => srcSFX.PlayOneShot(landClip,  MasterVolume * landVolume);

    // --- RUN LOOP ---
    public void StartRunLoop()
    {
        if (runClip == null || srcRun.isPlaying) return;
        srcRun.clip   = runClip;
        srcRun.volume = MasterVolume * runVolume;
        srcRun.pitch  = 1f;
        srcRun.Play();
    }

    public void StopRunLoop()
    {
        if (srcRun.isPlaying) srcRun.Stop();
    }

    // --- FALL LOOP ---
    public void StartFallLoop()
    {
        if (fallLoopCoroutine != null) return;
        fallLoopCoroutine = StartCoroutine(FallLoopRoutine());
    }

    public void StopFallLoop()
    {
        if (fallLoopCoroutine != null) { StopCoroutine(fallLoopCoroutine); fallLoopCoroutine = null; }
        // Không dừng srcSFX vì PlayOneShot không block
    }

    private IEnumerator FallLoopRoutine()
    {
        while (fallLoopClip != null)
        {
            srcSFX.PlayOneShot(fallLoopClip, MasterVolume * fallLoopVolume);
            yield return new WaitForSeconds(fallLoopClip.length);
        }
    }

    // ============================================================
    // ÂM THANH INVENTORY
    // ============================================================
    public void PlayEquip()          => srcUI.PlayOneShot(equipClip,          MasterVolume * equipVolume);
    public void PlayUnequip()        => srcUI.PlayOneShot(unequipClip,        MasterVolume * unequipVolume);
    public void PlayInventoryOpen()  => srcUI.PlayOneShot(inventoryOpenClip,  MasterVolume * inventoryOpenVolume);
    public void PlayInventoryClose() => srcUI.PlayOneShot(inventoryCloseClip, MasterVolume * inventoryCloseVolume);

    // ============================================================
    // ÂM THANH VFX / BOSS SKILLS (bị Time Stop)
    // ============================================================
    public void PlayVFX(int skillIndex)
    {
        if (vfxClips == null || skillIndex < 0 || skillIndex >= vfxClips.Length) return;
        if (vfxClips[skillIndex] == null) return;
        srcWorld.PlayOneShot(vfxClips[skillIndex], MasterVolume * vfxVolume);
    }

    /// <summary>Phát trực tiếp 1 AudioClip bất kỳ (VFX không phải boss skill)</summary>
    public void PlayDirectClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        srcWorld.PlayOneShot(clip, MasterVolume * volumeScale);
    }

    // ============================================================
    // ÂM THANH BOSS
    // ============================================================
    public void StartBossWingFlap(AudioClip clip)
    {
        if (clip == null || bossWingSource == null) return;
        bossWingSource.clip   = clip;
        bossWingSource.volume = MasterVolume * bossWingVolume;
        bossWingSource.Play();
    }

    public void StopBossWingFlap()
    {
        if (bossWingSource != null) bossWingSource.Stop();
    }

    // ============================================================
    // ÂM THANH HỆ THỐNG
    // ============================================================
    // Perfect Dodge dùng srcUI (không bị ApplyTimeStop pitch)
    public void PlayPerfectDodge() => srcUI.PlayOneShot(perfectDodgeClip, MasterVolume * perfectDodgeVolume);
    public void PlayLevelUp()      => srcUI.PlayOneShot(levelUpClip,      MasterVolume * levelUpVolume);
    public void PlayGameOver()     => srcUI.PlayOneShot(gameOverClip,     MasterVolume * gameOverVolume);
    public void PlayUIClick()      => srcUI.PlayOneShot(uiClickClip,      MasterVolume * uiClickVolume);

    // ============================================================
    // TIME STOP — Bóp pitch của srcWorld & bossWingSource
    // srcSFX (player) KHÔNG bị ảnh hưởng
    // ============================================================
    public void ApplyTimeStop(float slowFactor)
    {
        if (isTimeStopActive) return;
        isTimeStopActive = true;
        const float TIME_STOP_PITCH = 0.5f; // Luôn làm chậm 50%
        srcWorld.pitch       = TIME_STOP_PITCH;
        bossWingSource.pitch = TIME_STOP_PITCH;
        srcAmbient.pitch     = TIME_STOP_PITCH;
    }

    public void RevertTimeStop()
    {
        isTimeStopActive     = false;
        srcWorld.pitch       = normalPitch;
        bossWingSource.pitch = normalPitch;
        srcAmbient.pitch     = normalPitch;
    }

    // ============================================================
    // SAVE / LOAD SETTINGS
    // ============================================================
    public void LoadSettings(float sliderValue, bool muted)
    {
        masterSliderValue = sliderValue;
        isMuted = muted;
        ApplyVolumeToContinuousSources();
    }
}
