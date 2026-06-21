using UnityEngine;

namespace Soulslike.Player
{
    /// <summary>
    /// SMB on the Dodge state. Keeps root motion OFF so the directional dodge clip plays
    /// in place — PlayerDodge scripts the actual travel (the Mixamo dodge clips bake diagonal
    /// root motion that can't be aimed by forwarding). Explicit so a stray applyRootMotion
    /// can't leak the diagonal clip translation into the body.
    /// </summary>
    public class DodgingTagBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.applyRootMotion = false;
        }
    }
}
