using UnityEngine;

namespace Soulslike.AI
{
    public class HurtRootMotionBehaviour : StateMachineBehaviour
    {
        private bool prevRootMotion;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            prevRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.applyRootMotion = prevRootMotion;
        }
    }
}
