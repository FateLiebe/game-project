using UnityEngine;

public class Hurtbox : MonoBehaviour, IDamageable
{
    [SerializeField] private BaseEntity owner;
    public bool isPerfectDodging = false;

    public void TakeDamage(DamageInfo info)
    {
        if (isPerfectDodging)
        {
            // [FIX #2]: KHÔNG TẮT isPerfectDodging Ở ĐÂY NỮA
            // Để Coroutine PerfectDodgeWindowActive() tự động tắt sau 0.4s

            PlayerController pc = owner as PlayerController;
            if (pc != null)
            {
                BaseEntity attackerEntity = null;
                if (info.attacker != null)
                {
                    attackerEntity = info.attacker.GetComponent<BaseEntity>();
                }
                
                pc.OnPerfectDodgeSuccess(attackerEntity); 
            }
            return; 
        }

        if (owner != null) 
        {
            owner.ApplyDamage(info);
        }
    }
}