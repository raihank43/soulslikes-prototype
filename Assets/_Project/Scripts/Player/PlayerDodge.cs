using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Soulslike.Input;
using Soulslike.Combat;

namespace Soulslike.Player
{
    /// <summary>
    /// Dodge roll with i-frames. On the Player root. HYBRID: forward/back play a dive clip and
    /// "turn-and-roll" (the character rotates so the dive's baked travel, clipTravelAngle, points
    /// where you aimed — it turns INTO the roll, so facing isn't preserved mid-roll). Left/right
    /// play directional sidestep clips and KEEP facing. Either way the clip's OWN root motion
    /// carries the body (forwarded by RootMotionForwarder) — no scripted velocity, no body-vs-travel
    /// mismatch. The clip is picked via DodgeX/DodgeY in a blend tree.
    ///
    /// i-frames are driven by THIS coroutine on absolute time (not animation events): a dropped
    /// EndIFrames event would leave the player permanently invulnerable. The window is guaranteed
    /// to close (try/finally + OnDisable), and is deterministically testable.
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
        [Tooltip("Direction (deg, relative to the character's facing) the dodge clip's baked root " +
                 "motion travels. Measured from the clip; the character is rotated so this points " +
                 "where you aimed. Standing Dive Forward measured ~-36.")]
        [SerializeField] private float clipTravelAngle = -36f;
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

        /// <summary>Attempts a dodge. Returns true if one started. Called by input and by tests.</summary>
        public bool TryDodge()
        {
            if (IsDodging) return false;
            if (health != null && health.IsDead) return false;
            if (Time.time - lastDodgeTime < dodgeCooldown) return false;
            if (stamina != null && !stamina.TrySpend(staminaCost)) return false;

            AimDodge();
            if (animator != null) animator.SetTrigger(DodgeTriggerHash);
            lastDodgeTime = Time.time;
            routine = StartCoroutine(DodgeRoutine());
            return true;
        }

        // Hybrid aim: forward/back play the dive and turn-and-roll (rotate so the dive's baked travel
        // points where you aimed); left/right play the directional sidestep clips and KEEP facing
        // (their own root motion carries the lateral hop — sidesteps shouldn't spin). DodgeX/DodgeY
        // pick the clip in the blend tree.
        private void AimDodge()
        {
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
            if (worldDir.sqrMagnitude < 0.0001f) return;

            // Cardinal relative to current facing decides the clip: lateral = sidestep, else = dive.
            Vector3 local = transform.InverseTransformDirection(worldDir);
            bool lateral = Mathf.Abs(local.x) > Mathf.Abs(local.z);
            if (animator != null)
            {
                animator.SetFloat(DodgeXHash, lateral ? Mathf.Sign(local.x) : 0f);
                animator.SetFloat(DodgeYHash, lateral ? 0f : Mathf.Sign(local.z));
            }

            if (!lateral)
            {
                // Forward/back roll: rotate so the dive's baked travel (clipTravelAngle) hits the aim.
                float aimYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, aimYaw - clipTravelAngle, 0f);
                if (body != null) body.MoveRotation(rot);
                else transform.rotation = rot;
            }
            // Left/right sidestep: keep facing; the clip's own root motion does the lateral hop.
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
            }
        }
    }
}
