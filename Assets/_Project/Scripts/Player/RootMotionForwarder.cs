using UnityEngine;

namespace Soulslike.Player
{
    [RequireComponent(typeof(Animator))]
    public class RootMotionForwarder : MonoBehaviour
    {
        [SerializeField] private Rigidbody parentBody;
        // Forwards baked root motion for these animator tags. Dodge (turn-and-roll) uses the dodge
        // clip's real travel; PlayerDodge rotates the character to aim it where the player pointed.
        [SerializeField] private string[] activeTags = { "Attacking", "Dodging" };

        private Animator anim;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (parentBody == null) parentBody = GetComponentInParent<Rigidbody>();
        }

        private void OnAnimatorMove()
        {
            if (anim == null || parentBody == null) return;
            var state = anim.GetCurrentAnimatorStateInfo(0);
            bool active = false;
            for (int i = 0; i < activeTags.Length; i++)
            {
                if (state.IsTag(activeTags[i])) { active = true; break; }
            }
            if (!active) return;

            // Vertical baked into Mixamo attack clips (up to 20cm on Heavy) lifts the
            // body off the ground. Y is owned by physics — feed the rb only XZ.
            Vector3 delta = anim.deltaPosition;
            delta.y = 0f;
            parentBody.MovePosition(parentBody.position + delta);
        }
    }
}
