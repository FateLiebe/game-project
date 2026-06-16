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

    private void Start()
    {
        if (AudioManager.Instance == null) return;

        if (directClip != null)
            // Chế độ 1: Phát clip kéo thẳng vào
            AudioManager.Instance.PlayDirectClip(directClip, volume);
        else
            // Chế độ 2: Phát theo skillIndex
            AudioManager.Instance.PlayVFX(skillIndex);
    }
}
