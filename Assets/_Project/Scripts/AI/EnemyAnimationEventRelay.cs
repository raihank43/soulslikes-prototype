using UnityEngine;

namespace Soulslike.AI
{
    public class EnemyAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private EnemyAttack attack;

        private void Awake()
        {
            if (attack == null) attack = GetComponentInParent<EnemyAttack>();
        }

        public void EnableHitbox(int damage)
        {
            if (attack != null) attack.EnableHitbox(damage);
        }

        public void DisableHitbox()
        {
            if (attack != null) attack.DisableHitbox();
        }

        public void AttackComplete()
        {
            if (attack != null) attack.AttackComplete();
        }
    }
}
