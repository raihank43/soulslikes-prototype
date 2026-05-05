using UnityEngine;

namespace Soulslike.Player
{
    [RequireComponent(typeof(Animator))]
    public class RootMotionForwarder : MonoBehaviour
    {
        [SerializeField] private Rigidbody parentBody;
        [SerializeField] private string activeTag = "Attacking";

        private Animator anim;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (parentBody == null) parentBody = GetComponentInParent<Rigidbody>();
        }

        private void OnAnimatorMove()
        {
            if (anim == null || parentBody == null) return;
            if (!anim.GetCurrentAnimatorStateInfo(0).IsTag(activeTag)) return;

            // Vertical baked into Mixamo attack clips (up to 20cm on Heavy) lifts the
            // body off the ground. Y is owned by physics — feed the rb only XZ.
            Vector3 delta = anim.deltaPosition;
            delta.y = 0f;
            parentBody.MovePosition(parentBody.position + delta);
        }
    }
}
