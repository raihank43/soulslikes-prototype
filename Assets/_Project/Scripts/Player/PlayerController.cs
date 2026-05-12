using UnityEngine;
using UnityEngine.InputSystem;
using Soulslike.Input;
using Soulslike.Combat;

namespace Soulslike.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float sprintStickThreshold = 0.95f;
        [SerializeField] private float moveDeadzone = 0.3f;
        [SerializeField] private float rotationAngleDeadband = 1.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private float speedDampTime = 0.1f;
        [SerializeField] private float strafeDampTime = 0.1f;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private LockOnSystem lockOn;
        [SerializeField] private PlayerHealth health;

        private Rigidbody rb;
        private PlayerControls controls;
        private Vector2 moveInput;
        private bool sprintToggled;
        private bool wasAttacking;

        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int MoveXHash    = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash    = Animator.StringToHash("MoveY");
        private static readonly int IsLockedHash = Animator.StringToHash("IsLocked");

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (lockOn == null)
            {
                lockOn = GetComponent<LockOnSystem>();
            }
            if (health == null)
            {
                health = GetComponent<PlayerHealth>();
            }

            controls = new PlayerControls();
            controls.Player.Move.performed += OnMovePerformed;
            controls.Player.Move.canceled += OnMoveCanceled;
            controls.Player.Sprint.performed += OnSprintPerformed;
        }

        private void OnEnable()
        {
            controls.Player.Enable();
            if (health != null) health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            controls.Player.Disable();
            if (health != null) health.Died -= OnPlayerDied;
        }

        private void OnPlayerDied()
        {
            rb.linearVelocity = Vector3.zero;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (controls != null)
            {
                controls.Player.Move.performed -= OnMovePerformed;
                controls.Player.Move.canceled -= OnMoveCanceled;
                controls.Player.Sprint.performed -= OnSprintPerformed;
                controls.Dispose();
            }
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
        private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
        private void OnSprintPerformed(InputAction.CallbackContext ctx) => sprintToggled = !sprintToggled;

        private void FixedUpdate()
        {
            rb.angularVelocity = Vector3.zero;

            bool isLocked = lockOn != null && lockOn.IsLocked;
            if (animator != null) animator.SetBool(IsLockedHash, isLocked);

            bool isAttackingNow = animator != null && animator.GetCurrentAnimatorStateInfo(0).IsTag("Attacking");
            if (isAttackingNow)
            {
                if (!wasAttacking)
                {
                    Vector3 entryVel = rb.linearVelocity;
                    entryVel.x = 0f; entryVel.z = 0f;
                    rb.linearVelocity = entryVel;
                }
                wasAttacking = true;
                if (isLocked) FaceTarget();
                return;
            }
            wasAttacking = false;

            float h = moveInput.x;
            float v = moveInput.y;
            float rawMag = Mathf.Clamp01(moveInput.magnitude);

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            if (rawMag < moveDeadzone)
            {
                Vector3 stop = rb.linearVelocity;
                stop.x = 0f;
                stop.z = 0f;
                rb.linearVelocity = stop;
                WriteAnimSpeed(0f);
                WriteAnimStrafe(0f, 0f);
                if (isLocked) FaceTarget();
                return;
            }

            float remapped = Mathf.InverseLerp(moveDeadzone, 1f, rawMag);
            float inputMag = Mathf.Lerp(0.5f, 1f, remapped);

            Vector3 move = Vector3.ClampMagnitude(forward * v + right * h, 1f);
            bool isSprinting = sprintToggled && inputMag >= sprintStickThreshold && !isLocked;
            float groundSpeed = isSprinting ? sprintSpeed : walkSpeed * inputMag;

            Vector3 velocity = move.normalized * groundSpeed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;

            if (isLocked)
            {
                FaceTarget();
                Vector3 localDir = move.sqrMagnitude > 0.0001f
                    ? transform.InverseTransformDirection(move.normalized)
                    : Vector3.zero;
                float magScale = isSprinting ? 1f : inputMag;
                WriteAnimStrafe(Mathf.Clamp(localDir.x * magScale, -1f, 1f),
                                Mathf.Clamp(localDir.z * magScale, -1f, 1f));
                WriteAnimSpeed(0f);
            }
            else
            {
                if (move.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(move.normalized, Vector3.up);
                    float angleToTarget = Quaternion.Angle(rb.rotation, targetRot);
                    if (angleToTarget > rotationAngleDeadband)
                    {
                        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * 60f * Time.fixedDeltaTime));
                    }
                }
                float animSpeed = isSprinting ? 2f : inputMag;
                WriteAnimSpeed(animSpeed);
                WriteAnimStrafe(0f, 0f);
            }
        }

        private void FaceTarget()
        {
            if (lockOn == null || lockOn.CurrentTarget == null) return;
            Vector3 toTarget = lockOn.CurrentTarget.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            float angleToTarget = Quaternion.Angle(rb.rotation, targetRot);
            if (angleToTarget > rotationAngleDeadband)
            {
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * 60f * Time.fixedDeltaTime));
            }
        }

        private void WriteAnimSpeed(float target)
        {
            if (animator == null) return;
            animator.SetFloat(SpeedHash, target, speedDampTime, Time.fixedDeltaTime);
        }

        private void WriteAnimStrafe(float x, float y)
        {
            if (animator == null) return;
            animator.SetFloat(MoveXHash, x, strafeDampTime, Time.fixedDeltaTime);
            animator.SetFloat(MoveYHash, y, strafeDampTime, Time.fixedDeltaTime);
        }
    }
}
