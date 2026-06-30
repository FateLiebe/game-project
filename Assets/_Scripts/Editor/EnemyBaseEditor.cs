#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Custom Editor cho EnemyBase.
/// Mục đích: Ẩn thuộc tính "baseData" (được thừa kế từ BaseEntity) khỏi Inspector của các Quái vật,
/// vì Quái vật đã sử dụng "enemyData" và "bossData" riêng, tránh gây lỗi Missing và làm rác giao diện.
/// </summary>
[CustomEditor(typeof(EnemyBase), true)]
public class EnemyBaseEditor : Editor
{
    #region UNITY EDITOR LOGIC
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Vẽ tất cả các biến trên Inspector, NHƯNG NGOẠI TRỪ các biến của Player
        string[] propertiesToHide = new string[] 
        { 
            "m_Script", 
            "baseData",
            "currentStatPoints",
            "currentEXP",
            "expToNextLevel",
            "equipHealthBonus",
            "equipAttackBonus",
            "equipDefenseBonus",
            "equipCritRateBonus",
            "equipCritDamageBonus",
            "equipSpeedBonus",
            "addedHealthPoints",
            "addedAttackPoints",
            "addedDefensePoints",
            "addedCritPoints"
        };
        DrawPropertiesExcluding(serializedObject, propertiesToHide);

        serializedObject.ApplyModifiedProperties();

        // HIỂN THỊ CÁC THÔNG SỐ CỦA DATA NGAY TRÊN INSPECTOR
        EnemyBase myTarget = (EnemyBase)target;
        bool isBoss = myTarget is BossController;

        // Chỉ hiển thị các thông số Tầm nhìn & Khoảng cách của EnemyData nếu ĐÂY KHÔNG PHẢI LÀ BOSS.
        // Vì Boss dùng các chỉ số Range và Movement riêng biệt trên script BossController.
        if (myTarget.enemyData != null && !isBoss)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("--- ENEMY DATA (Khoảng cách & Tầm nhìn) ---", EditorStyles.boldLabel);
            
            SerializedObject dataSO = new SerializedObject(myTarget.enemyData);
            dataSO.Update();

            EditorGUI.BeginChangeCheck();

            SerializedProperty lineOfSight = dataSO.FindProperty("lineOfSight");
            if (lineOfSight != null) EditorGUILayout.PropertyField(lineOfSight);

            SerializedProperty attackRange = dataSO.FindProperty("attackRange");
            if (attackRange != null) EditorGUILayout.PropertyField(attackRange);

            SerializedProperty rangedAttackRange = dataSO.FindProperty("rangedAttackRange");
            if (rangedAttackRange != null) EditorGUILayout.PropertyField(rangedAttackRange);

            if (EditorGUI.EndChangeCheck())
            {
                dataSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(myTarget.enemyData);
            }
        }
    }
    #endregion
}
#endif
