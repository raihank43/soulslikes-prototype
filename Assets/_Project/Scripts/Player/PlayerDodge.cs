using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Soulslike.Input;
using Soulslike.Combat;

namespace Soulslike.Player
{
    /// <summary>
    /// Dodge roll with i-frames. On the Player root. Directional: picks the dodge clip
    /// matching the input direction (in character-local space) and lets that clip's baked
    /// root motion carry the body — no rotation, so a locked-on dodge stays facing the target.
    ///
    /// i-frames are driven by THIS coroutine on absolute time (not animation events): a
    /// dropped EndIFrames event would leave the player permanently invulnerable. The window
    /// is guaranteed to close (try/finally + OnDisable), and is deterministically testable.
    /// </summary>
    [RequireComponent(typeof(PlayerStamina))]
    public class PlayerDodge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Transform cameraTransform;

        [Header("Tuning")]
        [SerializeField] private float staminaCost = 30f;
        [SerializeField] private float iFrameStart = 0.2f;   // seconds into the dodge
        [SerializeField] private float iFrameEnd = 0.55f;    // seconds into the dodge
        [SerializeField] private float dodgeDuration = 0.9f; // input-lock length; set from clip length
        [SerializeField] private float dodgeSpeed = 3.5f;    // scripted slide speed (m/s); distance ≈ speed × duration
        [SerializeField] private float dodgeCooldown = 0.3f;

        public bool IsDodging { get; private set; }
        public float IFrameStart => iFrameStart;
        public float IFrameEnd => iFrameEnd;

        private static readonly int DodgeTriggerHash = Animator.StringToHash("DodgeTrigger");
        private static readonly int DodgeXHash = Animator.StringToHash("DodgeX");
        private static readonly int DodgeYHash = Animator.StringToHash("DodgeY");

        private PlayerControls controls;
        private Coroutine routine;
        private float lastDodgeTime = -999f;
        private Vector3 dodgeDir; // committed world XZ direction for the scripted slide

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();
            if (health == null) health = GetComponent<PlayerHealth>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            controls = new PlayerControls();
            controls.Player.Dodge.performed += OnDodgePressed;
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
            // Guarantee invulnerability never sticks if we're torn down mid-roll.
            if (routine != null) { StopCoroutine(routine); routine = null; }
            IsDodging = false;
            if (health != null) health.IsInvulnerable = false;
        }

        private void OnPlayerDied() => enabled = false;

        private void OnDestroy()
        {
            if (controls != null)
            {
                controls.Player.Dodge.performed -= OnDodgePressed;
                controls.Dispose();
            }
        }

        private void OnDodgePressed(InputAction.CallbackContext ctx) => TryDodge();

        private void FixedUpdate()
        {
            // Scripted dodge travel: drive a constant horizontal velocity along the committed
            // direction (NOT MovePosition — on a non-kinematic body MovePosition compounds velocity
            // and doubles the distance). PlayerController yields the body while the Dodging tag is
            // active, so we own movement here. Y is left to gravity.
            if (IsDodging && body != null)
            {
                Vector3 v = dodgeDir * dodgeSpeed;
                v.y = body.linearVelocity.y;
                body.linearVelocity = v;
            }
        }

        /// <summary>Attempts a dodge. Returns true if one started. Called by input and by tests.</summary>
        public bool TryDodge()
        {
            if (IsDodging) return false;
            if (health != null && health.IsDead) return false;
            if (Time.time - lastDodgeTime < dodgeCooldown) return false;
            if (stamina != null && !stamina.TrySpend(staminaCost)) return false;

            SetDodgeDirection();
            if (animator != null) animator.SetTrigger(DodgeTriggerHash);
            lastDodgeTime = Time.time;
            routine = StartCoroutine(DodgeRoutine());
            return true;
        }

        private void SetDodgeDirection()
        {
            // Desired world direction: camera-relative move input, or backward if neutral.
            Vector2 moveInput = controls != null ? controls.Player.Move.ReadValue<Vector2>() : Vector2.zero;
            Vector3 worldDir;
            if (moveInput.sqrMagnitude > 0.04f)
            {
                Vector3 fwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
                fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();
                worldDir = (fwd * moveInput.y + right * moveInput.x).normalized;
            }
            else
            {
                worldDir = -transform.forward;
            }
            worldDir.y = 0f;
            worldDir.Normalize();
            dodgeDir = worldDir; // committed: the body slides straight this way (PlayerDodge.FixedUpdate)

            // Local-space, snapped to the dominant cardinal -> the blend tree plays one clip ~100%.
            Vector3 local = transform.InverseTransformDirection(worldDir);
            float x = local.x, y = local.z;
            if (Mathf.Abs(x) > Mathf.Abs(y)) { x = Mathf.Sign(x); y = 0f; }
            else { x = 0f; y = Mathf.Sign(y); }

            if (animator != null)
            {
                animator.SetFloat(DodgeXHash, x);
                animator.SetFloat(DodgeYHash, y);
            }
        }

        private IEnumerator DodgeRoutine()
        {
            IsDodging = true;
            try
            {
                if (iFrameStart > 0f) yield return new WaitForSeconds(iFrameStart);
                if (health != null) health.IsInvulnerable = true;

                yield return new WaitForSeconds(Mathf.Max(0f, iFrameEnd - iFrameStart));
                if (health != null) health.IsInvulnerable = false;

                yield return new WaitForSeconds(Mathf.Max(0f, dodgeDuration - iFrameEnd));
            }
            finally
            {
                // Runs even if the coroutine is stopped (death/disable) — invuln can never stick.
                if (health != null) health.IsInvulnerable = false;
                IsDodging = false;
                routine = null;
                // Kill any slide carryover so the dodge stops cleanly instead of drifting.
                if (body != null)
                {
                    Vector3 v = body.linearVelocity;
                    v.x = 0f; v.z = 0f;
                    body.linearVelocity = v;
                }
            }
        }
    }
}
