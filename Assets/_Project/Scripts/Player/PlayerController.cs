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

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private Rigidbody rb;
        private PlayerControls controls;
        private Vector2 moveInput;
        private bool sprintToggled;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
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
            float h = moveInput.x;
            float v = moveInput.y;

            if (moveInput.sqrMagnitude < 0.0001f)
            {
                Vector3 stop = rb.linearVelocity;
                stop.x = 0f;
                stop.z = 0f;
                rb.linearVelocity = stop;
                return;
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = Vector3.ClampMagnitude(forward * v + right * h, 1f);
            float speed = sprintToggled ? sprintSpeed : walkSpeed;

            Vector3 velocity = move * speed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;

            Quaternion targetRot = Quaternion.LookRotation(move.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}
