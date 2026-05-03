using UnityEngine;
using Soulslike.Combat;

namespace Soulslike.Player
{
    [RequireComponent(typeof(Animator))]
    public class HeadLookAtIK : MonoBehaviour
    {
        [SerializeField] private LockOnSystem lockOn;
        [SerializeField] private float overallWeight = 1f;
        [SerializeField] private float bodyWeight = 0.05f;
        [SerializeField] private float headWeight = 1f;
        [SerializeField] private float eyesWeight = 0f;
        [SerializeField] private float clavicleWeight = 0.2f;
        [SerializeField] private float lookHeightOffset = 1.2f;
        [SerializeField] private float damping = 8f;

        private Animator anim;
        private Vector3 smoothedLookPos;
        private float smoothedWeight;

        private void Awake()
        {
            anim = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            bool active = lockOn != null && lockOn.IsLocked && lockOn.CurrentTarget != null;
            Vector3 desiredPos = active
                ? lockOn.CurrentTarget.position + Vector3.up * lookHeightOffset
                : transform.position + transform.forward * 5f + Vector3.up * 1.6f;

            if (smoothedLookPos == Vector3.zero) smoothedLookPos = desiredPos;
            smoothedLookPos = Vector3.Lerp(smoothedLookPos, desiredPos, Time.deltaTime * damping);

            float targetWeight = active ? overallWeight : 0f;
            smoothedWeight = Mathf.Lerp(smoothedWeight, targetWeight, Time.deltaTime * damping);

            anim.SetLookAtPosition(smoothedLookPos);
            anim.SetLookAtWeight(smoothedWeight, bodyWeight, headWeight, eyesWeight, clavicleWeight);
        }
    }
}
