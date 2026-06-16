using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// AudioManager — Singleton quản lý TOÀN BỘ âm thanh game.
/// Gắn vào GameObject "AudioManager" trong scene Core_Gameplay.
/// DontDestroyOnLoad nên sống xuyên suốt các map.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ============================================================
    // AUDIO SOURCES — mỗi "kênh" là 1 AudioSource riêng biệt
    // ============================================================
    [Header("Audio Sources (tự động tạo, không cần kéo thả)")]
    private AudioSource srcAmbient;   // Phát âm thanh môi trường
    private AudioSource srcUI;        // Âm thanh UI / hệ thống
    private AudioSource srcSFX;       // Âm thanh player / combat ngắn

    // ============================================================
    // ÂM THANH MÔI TRƯỜNG
    // Kéo 8 file .wav/.ogg vào đây trong Inspector
    // ============================================================
    [Header("🌿 Âm Thanh Môi Trường (kéo 8 file vào)")]
    public AudioClip[] ambientClips;   // 8 file âm thanh môi trường

    // ============================================================
    // ÂM THANH PLAYER
    // Kéo từng file vào đúng ô tương ứng
    // ============================================================
    [Header("🗡️ Âm Thanh Player")]
    public AudioClip[] attackCombo;    // [0]=combo1, [1]=combo2, [2]=combo3
    public AudioClip dashClip;
    public AudioClip fallLoopClip;     // Lặp lại khi lơ lửng > 1.5s
    public AudioClip jumpClip;
    public AudioClip landClip;         // Âm đáp đất (sau nhảy hoặc rơi)
    public AudioClip runClip;          // Chạy (loop)
    public AudioClip runToIdleClip;    // Khi dừng lại từ trạng thái chạy

    // ============================================================
    // ÂM THANH INVENTORY
    // ============================================================
    [Header("🎒 Âm Thanh Inventory")]
    public AudioClip inventoryOpenClip;  // Mở túi đồ
    public AudioClip inventoryCloseClip; // Đóng túi đồ
    public AudioClip equipClip;          // Mặc trang bị
    public AudioClip unequipClip;        // Cởi trang bị

    // ============================================================
    // ÂM THANH VFX
    // Kéo file âm thanh của từng VFX skill (theo thứ tự skill 0-6)
    // ============================================================
    [Header("✨ Âm Thanh VFX / Boss Skills (theo thứ tự skill 0-6)")]
    public AudioClip[] vfxClips;       // index tương ứng với skillIndex của Boss

    // ============================================================
    // ÂM THANH BOSS
    // ============================================================
    [Header("👾 Âm Thanh Boss")]
    public AudioClip bossWingFlapClip; // Vỗ cánh — phát LOOP liên tục khi boss sống

    // ============================================================
    // ÂM THANH HỆ THỐNG
    // ============================================================
    [Header("⚙️ Âm Thanh Hệ Thống")]
    public AudioClip perfectDodgeClip; // Kích hoạt Time Stop
    public AudioClip gameOverClip;     // Người chơi die
    public AudioClip uiClickClip;      // Bấm nút UI

    // ============================================================
    // CÀI ĐẶT ÂM LƯỢNG (điều khiển từ PauseScreen)
    // ============================================================
    [Header("🔊 Cài Đặt Âm Lượng")]
    [Range(0f, 1f)] public float masterVolume = 1f;  // Lưu nội bộ (0-1)
    public bool isMuted = false;

    // ============================================================
    // TRẠNG THÁI NỘI BỘ
    // ============================================================
    private Coroutine ambientCycleCoroutine;
    private AudioSource bossWingSource;    // AudioSource riêng cho boss wing (loop)

    private int[] lastThreeAmbient = new int[] { -1, -1, -1 }; // Tránh trùng lặp
    private int ambientHistoryIndex = 0;

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
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateAudioSources()
    {
        // Tạo các AudioSource tự động — không cần kéo thả trong Inspector
        srcAmbient = gameObject.AddComponent<AudioSource>();
        srcAmbient.loop = false;
        srcAmbient.playOnAwake = false;

        srcSFX = gameObject.AddComponent<AudioSource>();
        srcSFX.loop = false;
        srcSFX.playOnAwake = false;

        srcUI = gameObject.AddComponent<AudioSource>();
        srcUI.loop = false;
        srcUI.playOnAwake = false;

        // Boss wing cần source riêng vì nó loop độc lập
        bossWingSource = gameObject.AddComponent<AudioSource>();
        bossWingSource.loop = true;
        bossWingSource.playOnAwake = false;
    }

    // ============================================================
    // VOLUME CONTROL — gọi từ PauseScreen
    // Nhận value từ 1-10 (người dùng thấy), chuyển nội bộ về 0-1
    // ============================================================

    /// <summary>Gọi từ Slider (1-10) trên PauseScreen</summary>
    public void SetVolume(float sliderValue)
    {
        masterVolume = Mathf.Clamp(sliderValue, 1f, 10f) / 10f;
        ApplyVolume();
    }

    /// <summary>Gọi từ Toggle trên PauseScreen</summary>
    public void SetMute(bool muted)
    {
        isMuted = muted;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        float vol = isMuted ? 0f : masterVolume;
        srcAmbient.volume    = vol * 0.6f;  // Môi trường nhỏ hơn một chút
        srcSFX.volume        = vol;
        srcUI.volume         = vol;
        bossWingSource.volume = vol * 0.5f;
    }

    // ============================================================
    // ÂM THANH MÔI TRƯỜNG — chu kỳ: nghỉ → phát → nghỉ
    // ============================================================

    /// <summary>
    /// Gọi khi Player chuyển map (từ GameLoader hoặc script map).
    /// Hủy chu kỳ cũ, bắt đầu chu kỳ mới ngay lập tức.
    /// </summary>
    public void RestartAmbientCycle()
    {
        if (ambientCycleCoroutine != null) StopCoroutine(ambientCycleCoroutine);
        srcAmbient.Stop();
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
            // --- NGHỈ NGẪU NHIÊN (7~9 giây) ---
            float waitTime = Random.Range(7f, 9f);
            yield return new WaitForSeconds(waitTime);

            // --- CHỌN FILE NGẪU NHIÊN (không trùng 3 lần gần nhất) ---
            if (ambientClips == null || ambientClips.Length == 0) continue;

            int chosen = PickAmbientClip();
            AudioClip clip = ambientClips[chosen];
            if (clip == null) continue;

            // --- PHÁT ---
            float vol = isMuted ? 0f : masterVolume * 0.6f;
            srcAmbient.volume = vol;
            srcAmbient.clip   = clip;
            srcAmbient.Play();

            // --- CHỜ HẾT FILE ---
            yield return new WaitForSeconds(clip.length);

            // --- NGHỈ LẦN 2 (7~9 giây) rồi lặp lại ---
            waitTime = Random.Range(7f, 9f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>Chọn index clip không trùng 3 lần gần nhất</summary>
    private int PickAmbientClip()
    {
        if (ambientClips.Length <= 3)
            return Random.Range(0, ambientClips.Length);

        int chosen;
        int safetyLoop = 0;
        do
        {
            chosen = Random.Range(0, ambientClips.Length);
            safetyLoop++;
        }
        while (System.Array.IndexOf(lastThreeAmbient, chosen) >= 0 && safetyLoop < 20);

        // Ghi lịch sử vòng xoay 3 lần
        lastThreeAmbient[ambientHistoryIndex % 3] = chosen;
        ambientHistoryIndex++;
        return chosen;
    }

    // ============================================================
    // ÂM THANH PLAYER
    // ============================================================

    public void PlayAttack(int comboStep)
    {
        // comboStep: 1, 2, 3 → index 0, 1, 2
        int idx = Mathf.Clamp(comboStep - 1, 0, (attackCombo?.Length ?? 1) - 1);
        PlaySFX(attackCombo != null && attackCombo.Length > idx ? attackCombo[idx] : null);
    }

    public void PlayDash()      => PlaySFX(dashClip);
    public void PlayJump()      => PlaySFX(jumpClip);
    public void PlayLand()      => PlaySFX(landClip);
    public void PlayRunToIdle() => PlaySFX(runToIdleClip);

    // Run loop — dùng srcAmbient phụ hoặc 1 source riêng? 
    // Để đơn giản, dùng srcSFX với PlayOneShot để không cắt combo/attack sound
    private Coroutine fallLoopCoroutine;

    public void StartFallLoop()
    {
        if (fallLoopCoroutine != null) return; // Đã đang phát
        fallLoopCoroutine = StartCoroutine(FallLoopRoutine());
    }

    public void StopFallLoop()
    {
        if (fallLoopCoroutine != null)
        {
            StopCoroutine(fallLoopCoroutine);
            fallLoopCoroutine = null;
        }
    }

    private IEnumerator FallLoopRoutine()
    {
        while (fallLoopClip != null)
        {
            float vol = isMuted ? 0f : masterVolume;
            srcSFX.PlayOneShot(fallLoopClip, vol);
            yield return new WaitForSeconds(fallLoopClip.length);
        }
    }

    // ============================================================
    // ÂM THANH INVENTORY
    // ============================================================
    public void PlayEquip()         => PlaySFX(equipClip);
    public void PlayUnequip()       => PlaySFX(unequipClip);
    public void PlayInventoryOpen() => PlayUI(inventoryOpenClip);
    public void PlayInventoryClose()=> PlayUI(inventoryCloseClip);

    // ============================================================
    // ÂM THANH VFX
    // Gọi từ VFX_SoundTrigger.cs (gắn lên từng VFX prefab)
    // skillIndex tương ứng với BossSkillManager
    // ============================================================
    public void PlayVFX(int skillIndex)
    {
        if (vfxClips == null || skillIndex < 0 || skillIndex >= vfxClips.Length) return;
        PlaySFX(vfxClips[skillIndex]);
    }

    /// <summary>Phát thẳng 1 AudioClip bất kỳ — dùng cho VFX không thuộc boss skill</summary>
    public void PlayDirectClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || srcSFX == null) return;
        float vol = isMuted ? 0f : masterVolume * volumeScale;
        srcSFX.PlayOneShot(clip, vol);
    }

    // ============================================================
    // ÂM THANH BOSS — WING FLAP LOOP
    // ============================================================

    /// <summary>Gọi từ BossAudio khi boss Awake/Start</summary>
    public void StartBossWingFlap(AudioClip clip)
    {
        if (clip == null || bossWingSource == null) return;
        bossWingSource.clip = clip;
        bossWingSource.volume = isMuted ? 0f : masterVolume * 0.5f;
        bossWingSource.Play();
    }

    /// <summary>Gọi từ BossAudio khi boss chết</summary>
    public void StopBossWingFlap()
    {
        if (bossWingSource != null) bossWingSource.Stop();
    }

    // ============================================================
    // ÂM THANH HỆ THỐNG
    // ============================================================
    public void PlayPerfectDodge() => PlayUI(perfectDodgeClip);
    public void PlayGameOver()     => PlayUI(gameOverClip);
    public void PlayUIClick()      => PlayUI(uiClickClip);

    // ============================================================
    // HÀM NỘI BỘ TIỆN ÍCH
    // ============================================================

    /// <summary>Phát SFX gameplay (attack, dash, jump...)</summary>
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || srcSFX == null) return;
        float vol = isMuted ? 0f : masterVolume;
        srcSFX.PlayOneShot(clip, vol);
    }

    /// <summary>Phát SFX UI / hệ thống</summary>
    private void PlayUI(AudioClip clip)
    {
        if (clip == null || srcUI == null) return;
        float vol = isMuted ? 0f : masterVolume;
        srcUI.PlayOneShot(clip, vol);
    }
}
