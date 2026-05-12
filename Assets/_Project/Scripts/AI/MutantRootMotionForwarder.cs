using UnityEngine;
using UnityEngine.AI;

namespace Soulslike.AI
{
    [RequireComponent(typeof(Animator))]
    public class MutantRootMotionForwarder : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator anim;

        private void Awake()
        {
            if (anim == null) anim = GetComponent<Animator>();
            if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        }

        // Animator calls this whenever it would apply root motion. We gate on applyRootMotion
        // so non-root-motion states (Locomotion driven by NavMeshAgent) are unaffected.
        private void OnAnimatorMove()
        {
            if (anim == null || !anim.applyRootMotion) return;
            Vector3 delta = anim.deltaPosition;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(delta);
            }
            else
            {
                // Dead path: NavMeshAgent disabled in OnDied, fall back to direct translate.
                transform.parent.position += delta;
            }
            // Apply rotation regardless (animator handles rotation deltas)
            transform.parent.rotation *= anim.deltaRotation;
        }
    }
}
