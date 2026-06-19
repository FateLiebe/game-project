using UnityEngine;

/// <summary>
/// VFX_SoundTrigger — Gắn lên bất kỳ VFX prefab nào.
/// Khi VFX được Instantiate, tự động phát âm thanh.
///
/// ► CHẾ ĐỘ 1 — Direct Clip (VFX độc lập, không phải boss skill):
///     Kéo AudioClip thẳng vào ô "Direct Clip" → xong!
///     Không cần đăng ký gì trong AudioManager.
///
/// ► CHẾ ĐỘ 2 — Skill Index (VFX của boss skill):
///     Để Direct Clip = None, điền Skill Index = số skill (0→6).
///     AudioClip lấy từ AudioManager.vfxClips[skillIndex].
/// </summary>
public class VFX_SoundTrigger : MonoBehaviour
{
    [Tooltip("Chế độ 1: Kéo AudioClip thẳng vào đây (ưu tiên hơn Skill Index)")]
    public AudioClip directClip;

    [Tooltip("Chế độ 2: Dùng khi Direct Clip = None. Index tương ứng AudioManager.vfxClips[]")]
    public int skillIndex = 0;

    [Tooltip("Âm lượng phát (0-1), mặc định = 1")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Cài đặt tối ưu")]
    [Tooltip("Bật cái này nếu muốn âm thanh tự tắt khi đạn nổ/biến mất (ví dụ đạn bay). Tắt đi cho các chiêu như Electro-shock")]
    public bool stopAudioOnDestroy = false;

    private AudioSource mySource;

    private void OnEnable()
    {
        if (AudioManager.Instance == null) return;

        AudioClip clipToPlay = directClip;
        
        if (clipToPlay == null && AudioManager.Instance.vfxClips != null)
        {
            if (skillIndex >= 0 && skillIndex < AudioManager.Instance.vfxClips.Length)
                clipToPlay = AudioManager.Instance.vfxClips[skillIndex];
        }

        if (clipToPlay == null) return;

        if (stopAudioOnDestroy)
        {
            // Nếu muốn tự tắt, tạo hoặc tái sử dụng AudioSource riêng để quản lý
            if (mySource == null) mySource = gameObject.AddComponent<AudioSource>();
            
            mySource.clip = clipToPlay;
            mySource.volume = AudioManager.Instance.MasterVolume * volume;
            mySource.spatialBlend = 0f; // 2D sound
            mySource.Play();
        }
        else
        {
            // Phát thả ga (OneShot) như cũ, phù hợp cho Electro-shock
            if (directClip != null)
                AudioManager.Instance.PlayDirectClip(directClip, volume);
            else
                AudioManager.Instance.PlayVFX(skillIndex);
        }
    }

    private void Update()
    {
        // Cập nhật âm lượng và Time Stop nếu dùng AudioSource riêng
        if (mySource != null && AudioManager.Instance != null)
        {
            mySource.volume = AudioManager.Instance.MasterVolume * volume;
            mySource.pitch = AudioManager.Instance.IsTimeStopActive ? 0.5f : 1f;
        }
    }

    private void OnDisable()
    {
        if (stopAudioOnDestroy && mySource != null)
        {
            mySource.Stop();
        }
    }
}
