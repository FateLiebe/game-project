using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Data/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Core Stats")]
    public float maxHealth = 100f;
    public float defense = 0f;

    [Header("Movement & Jump Stats")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    public float fastFallSpeed = 15f;
    public int maxJumps = 2;

    [Header("Dash Stats")]
    public float dashForce = 22f;
    public float dashTime = 0.2f;
    public float dashStallTime = 0.3f; 
    public float dashRechargeTime = 1.25f;
    public int maxDashes = 2;
    public int maxAirDashes = 2;

    [Header("Combat & Input Settings")]
    public float comboResetTime = 1.2f;
    public float doubleTapThreshold = 0.25f;
}