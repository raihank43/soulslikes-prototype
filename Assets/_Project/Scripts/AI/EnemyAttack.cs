using UnityEngine;

namespace Soulslike.AI
{
    public class EnemyAttack : MonoBehaviour
    {
        [SerializeField] private EnemyHitbox hitbox;

        public bool IsAttackComplete { get; private set; }

        public void BeginAttack()
        {
            IsAttackComplete = false;
        }

        public void EnableHitbox(int damage)
        {
            if (hitbox != null) hitbox.Enable(damage);
        }

        public void DisableHitbox()
        {
            if (hitbox != null) hitbox.Disable();
        }

        public void AttackComplete()
        {
            IsAttackComplete = true;
            if (hitbox != null) hitbox.Disable();
        }

        public void ForceCancel()
        {
            if (hitbox != null) hitbox.Disable();
            IsAttackComplete = true;
        }
    }
}
