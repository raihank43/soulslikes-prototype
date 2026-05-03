using UnityEngine;
using UnityEngine.InputSystem;
using Soulslike.Input;

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

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private Rigidbody rb;
        private PlayerControls controls;
        private Vector2 moveInput;
        private bool sprintToggled;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

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

            controls = new PlayerControls();
            controls.Player.Move.performed += OnMovePerformed;
            controls.Player.Move.canceled += OnMoveCanceled;
            controls.Player.Sprint.performed += OnSprintPerformed;
        }

        private void OnEnable()
        {
            controls.Player.Enable();
        }

        private void OnDisable()
        {
            controls.Player.Disable();
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

            float h = moveInput.x;
            float v = moveInput.y;
            float rawMag = Mathf.Clamp01(moveInput.magnitude);

            if (rawMag < moveDeadzone)
            {
                Vector3 stop = rb.linearVelocity;
                stop.x = 0f;
                stop.z = 0f;
                rb.linearVelocity = stop;
                WriteAnimatorSpeed(0f);
                return;
            }

            float remapped = Mathf.InverseLerp(moveDeadzone, 1f, rawMag);
            float inputMag = Mathf.Lerp(0.5f, 1f, remapped);

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = Vector3.ClampMagnitude(forward * v + right * h, 1f);

            bool isSprinting = sprintToggled && inputMag >= sprintStickThreshold;
            float groundSpeed = isSprinting ? sprintSpeed : walkSpeed * inputMag;
            float animSpeed = isSprinting ? 2f : inputMag;

            Vector3 velocity = move.normalized * groundSpeed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;

            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(move.normalized, Vector3.up);
                float angleToTarget = Quaternion.Angle(rb.rotation, targetRot);
                if (angleToTarget > rotationAngleDeadband)
                {
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * 60f * Time.fixedDeltaTime));
                }
            }

            WriteAnimatorSpeed(animSpeed);
        }

        private void WriteAnimatorSpeed(float target)
        {
            if (animator == null) return;
            animator.SetFloat(SpeedHash, target, speedDampTime, Time.fixedDeltaTime);
        }
    }
}
