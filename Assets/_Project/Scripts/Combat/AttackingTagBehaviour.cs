using UnityEngine;

namespace Soulslike.Combat
{
    public class AttackingTagBehaviour : StateMachineBehaviour
    {
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int ComboReadyHash = Animator.StringToHash("ComboReady");
        private static readonly int AttackingTagHash = Animator.StringToHash("Attacking");
        private static readonly int LungeSpeedHash = Animator.StringToHash("AttackLungeSpeed");

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.SetBool(IsAttackingHash, true);
            animator.SetBool(ComboReadyHash, false);
            // Lunge stays 0 by default; Animation Events drive it during the strike window only.
            animator.SetFloat(LungeSpeedHash, 0f);
            animator.applyRootMotion = true;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // OnStateExit fires AFTER the destination state's OnStateEnter when transitioning between attack states.
            // If we're chaining Light1 -> Light2, Light1's exit must NOT clobber the flags Light2 just set.
            var current = animator.GetCurrentAnimatorStateInfo(layerIndex);
            if (current.tagHash == AttackingTagHash) return;
            if (animator.IsInTransition(layerIndex) &&
                animator.GetNextAnimatorStateInfo(layerIndex).tagHash == AttackingTagHash) return;

            animator.SetBool(IsAttackingHash, false);
            animator.SetBool(ComboReadyHash, false);
            animator.SetFloat(LungeSpeedHash, 0f);
            animator.ResetTrigger("LightAttack");
            animator.ResetTrigger("HeavyAttack");
            animator.applyRootMotion = false;
        }
    }
}
