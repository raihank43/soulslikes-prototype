using UnityEngine;

namespace Soulslike.Player
{
    /// <summary>
    /// SMB on the Dodge state. Enables root motion so RootMotionForwarder carries the dodge clip's
    /// real baked travel (turn-and-roll: PlayerDodge rotates the character so that travel points
    /// where the player aimed). Restores applyRootMotion off on exit.
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
