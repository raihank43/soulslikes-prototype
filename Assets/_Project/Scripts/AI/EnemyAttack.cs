using UnityEngine;

namespace Soulslike.AI
{
    public enum EnemyAttackType { Punch, Swipe, JumpAttack }

    public class EnemyAttack : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyHitbox hitbox;

        private static readonly int PunchTriggerHash      = Animator.StringToHash("PunchTrigger");
        private static readonly int SwipeTriggerHash      = Animator.StringToHash("SwipeTrigger");
        private static readonly int JumpAttackTriggerHash = Animator.StringToHash("JumpAttackTrigger");

        public bool IsAttackComplete { get; private set; }
        public EnemyAttackType CurrentAttack { get; private set; }

        // Wall-clock swing durations per attack (in seconds, at AttackSpeed=1.0).
        // These match ~75% of each clip's true visible-swing length so the routine
        // returns to Chase right after the strike instead of holding the post-swing pose.
        public static float DurationFor(EnemyAttackType type)
        {
            switch (type)
            {
                case EnemyAttackType.Punch:      return 0.88f;
                case EnemyAttackType.Swipe:      return 2.00f;
                case EnemyAttackType.JumpAttack: return 2.80f;
                default: return 1.0f;
            }
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        public void BeginAttack(EnemyAttackType type)
        {
            IsAttackComplete = false;
            CurrentAttack = type;
            if (animator == null) return;
            switch (type)
            {
                case EnemyAttackType.Punch:      animator.SetTrigger(PunchTriggerHash); break;
                case EnemyAttackType.Swipe:      animator.SetTrigger(SwipeTriggerHash); break;
                case EnemyAttackType.JumpAttack: animator.SetTrigger(JumpAttackTriggerHash); break;
            }
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
