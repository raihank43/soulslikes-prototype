using UnityEngine;

namespace Soulslike.Player
{
    /// <summary>
    /// SMB on the Dodge state. Enables root motion on enter so RootMotionForwarder forwards
    /// the dodge clip's baked translation, and restores it on exit. Mirrors AttackingTagBehaviour
    /// but without combo flags — the dodge never chains.
    /// </summary>
    public class DodgingTagBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.applyRootMotion = true;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.applyRootMotion = false;
        }
    }
}
