using UnityEngine;

namespace Soulslike.AI
{
    public class EnemyAttackMotionBehaviour : StateMachineBehaviour
    {
        private bool wasApplyingRootMotion;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            wasApplyingRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.applyRootMotion = wasApplyingRootMotion;
        }
    }
}
