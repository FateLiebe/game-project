using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý cơ chế Dị Thường Thời Gian (Ngưng đọng thời gian/Time Stop).
/// Được kích hoạt chủ yếu thông qua kỹ năng Perfect Dodge (Né hoàn hảo).
/// Cơ chế này không dùng Time.timeScale = 0 (vì sẽ làm khựng cả người chơi), 
/// mà nó can thiệp trực tiếp vào từng thành phần của quái vật để làm chậm chúng.
/// </summary>
public class TimeAnomalyManager : MonoBehaviour
{
    #region VARIABLES & PROPERTIES
    public static TimeAnomalyManager Instance;

    [Header("Time Stop Settings")]
    [Tooltip("Hệ số làm chậm. Vd: 0.07 nghĩa là thời gian trôi với tốc độ 7% (giảm 93%)")]
    [SerializeField] private float slowFactor = 0.07f; 
    [Tooltip("Thời gian duy trì trạng thái Ngưng Đọng (tính theo giây thực tế)")]
    [SerializeField] private float duration = 3f;      
    #endregion

    #region UNITY LIFECYCLE
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region PUBLIC METHODS
    /// <summary>
    /// Hàm này được gọi từ PlayerController_Combat khi người chơi lướt (Dash) đúng lúc quái đánh.
    /// </summary>
    public void TriggerPerfectDodge()
    {
        AudioManager.Instance?.PlayPerfectDodge(); // Phát âm thanh điện xẹt của Perfect Dodge
        TriggerTimeStop(); // Kích hoạt làm chậm
    }

    /// <summary>
    /// Kích hoạt chuỗi sự kiện Ngưng đọng thời gian.
    /// Dừng các luồng đếm giờ cũ (nếu người chơi spam Perfect Dodge liên tục) và bắt đầu luồng mới.
    /// </summary>
    public void TriggerTimeStop()
    {
        StopAllCoroutines(); 
        AudioManager.Instance?.RevertTimeStop(); // Trả lại cao độ âm thanh (pitch) bình thường trước khi bóp lại
        StartCoroutine(TimeStopRoutine());
    }
    #endregion

    #region COROUTINES
    /// <summary>
    /// Coroutine chính xử lý quá trình Làm Chậm -> Chờ 3 giây -> Trả lại bình thường.
    /// </summary>
    private IEnumerator TimeStopRoutine()
    {
        // --- BƯỚC 1: QUÉT TÌM MỤC TIÊU ---
        // Sử dụng FindObjectsByType thay vì hàm cũ (FindObjectsOfType) để tối ưu và tránh Warning trên Unity 6
        BaseEntity[] allEntities = Object.FindObjectsByType<BaseEntity>(FindObjectsInactive.Exclude);
        Rigidbody2D[] allRbs = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude);
        Animator[] allAnims = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude);

        // Tạo danh sách tạm để lưu trữ những kẻ xấu số bị dính đòn (để lát nữa chỉ khôi phục cho những kẻ này)
        List<BaseEntity> affectedEntities = new List<BaseEntity>();
        List<Rigidbody2D> affectedRbs = new List<Rigidbody2D>();
        List<Animator> affectedAnims = new List<Animator>();

        // --- BƯỚC 2. BÓP ĐỒNG HỒ LOGIC (BaseEntity) ---
        // Làm chậm các bộ đếm giờ bên trong nội bộ của quái (thời gian chờ tấn công, thời gian hồi chiêu)
        foreach (var entity in allEntities)
        {
            if (!entity.CompareTag("Player")) // Tuyệt đối không làm chậm Player
            {
                affectedEntities.Add(entity);
                entity.timeMultiplier = slowFactor; // Ép hệ số nhân thời gian xuống 0.07
            }
        }

        // --- BƯỚC 3. BÓP VẬT LÝ TỰ DO (Rigidbody2D) ---
        // Làm chậm vận tốc rơi và lực bay của các vật thể (Ví dụ: mũi tên, quả cầu lửa đang bay)
        foreach (var rb in allRbs)
        {
            // Bỏ qua Player VÀ bỏ qua các vật thể tĩnh (Cái xác quái vật) để tránh Warning của Unity 6
            if (!rb.CompareTag("Player") && rb.bodyType != RigidbodyType2D.Static)
            {
                affectedRbs.Add(rb);
                rb.gravityScale *= slowFactor;  // Rơi chậm lại như trên mặt trăng
                
                // Tránh can thiệp vận tốc của Quái vật (vì Quái vật dùng Kinematic/MovePosition), 
                // Chỉ bóp vận tốc của các vật thể bay (Đạn) không có script BaseEntity.
                if (rb.GetComponent<BaseEntity>() == null)
                    rb.linearVelocity *= slowFactor; 
            }
        }

        // --- BƯỚC 4. BÓP KHUNG HÌNH (Animator) ---
        // Cho quái vật diễn hoạt ảnh như phim quay chậm (Slow-motion)
        foreach (var anim in allAnims)
        {
            if (!anim.CompareTag("Player"))
            {
                affectedAnims.Add(anim);
                anim.speed = slowFactor; 
            }
        }

        // --- BƯỚC 5: TẬN HƯỞNG ---
        AudioManager.Instance?.ApplyTimeStop(slowFactor); // Kéo giãn âm thanh (Pitch) xuống tạo cảm giác trầm đục u ám
        yield return new WaitForSeconds(duration);        // Đợi đúng 3 giây thời gian thực

        // --- BƯỚC 6: TRẢ LẠI THỜI GIAN NHƯ CŨ (REVERT) ---
        AudioManager.Instance?.RevertTimeStop(); // Đưa âm nhạc về bình thường
        
        // Trả tốc độ logic
        foreach (var entity in affectedEntities)
        {
            if (entity != null) entity.timeMultiplier = 1f;
        }

        // Trả vật lý
        foreach (var rb in affectedRbs)
        {
            if (rb != null)
            {
                rb.gravityScale /= slowFactor;
                // Nếu là đạn bay, trả lại vận tốc bay ban đầu
                if (rb.GetComponent<BaseEntity>() == null)
                    rb.linearVelocity /= slowFactor;
            }
        }

        // Trả hoạt ảnh
        foreach (var anim in affectedAnims)
        {
            if (anim != null) anim.speed = 1f;
        }
    }
    #endregion
}
