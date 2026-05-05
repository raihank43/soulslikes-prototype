using UnityEngine;
using UnityEngine.InputSystem;
using Soulslike.Input;

namespace Soulslike.Player
{
    [RequireComponent(typeof(PlayerStamina))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerStamina stamina;

        [Header("Costs")]
        [SerializeField] private float light1Cost = 22f;
        [SerializeField] private float light2Cost = 18f;
        [SerializeField] private float light3Cost = 28f;
        [SerializeField] private float heavyCost  = 45f;

        [Header("Tuning")]
        [SerializeField] private float inputAcceptedLockout = 0.15f;
        [SerializeField] private float bufferLifetime = 0.4f;

        private static readonly int LightAttackHash  = Animator.StringToHash("LightAttack");
        private static readonly int HeavyAttackHash  = Animator.StringToHash("HeavyAttack");
        private static readonly int ComboReadyHash   = Animator.StringToHash("ComboReady");
        private static readonly int AttackingTagHash = Animator.StringToHash("Attacking");

        private PlayerControls controls;
        private float lastAcceptedTime = -999f;
        private float pendingLightTime = -999f;
        private float pendingHeavyTime = -999f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();

            controls = new PlayerControls();
            controls.Player.LightAttack.performed += OnLightAttackPressed;
            controls.Player.HeavyAttack.performed += OnHeavyAttackPressed;
        }

        private void OnEnable() => controls.Player.Enable();
        private void OnDisable() => controls.Player.Disable();

        private void OnDestroy()
        {
            if (controls != null)
            {
                controls.Player.LightAttack.performed -= OnLightAttackPressed;
                controls.Player.HeavyAttack.performed -= OnHeavyAttackPressed;
                controls.Dispose();
            }
        }

        private void OnLightAttackPressed(InputAction.CallbackContext ctx)
        {
            if (!TryFireLight()) pendingLightTime = Time.time;
        }

        private void OnHeavyAttackPressed(InputAction.CallbackContext ctx)
        {
            if (!TryFireHeavy()) pendingHeavyTime = Time.time;
        }

        private void Update()
        {
            // Drain expired buffers
            if (Time.time - pendingLightTime > bufferLifetime) pendingLightTime = -999f;
            if (Time.time - pendingHeavyTime > bufferLifetime) pendingHeavyTime = -999f;

            if (pendingLightTime > 0f && TryFireLight()) pendingLightTime = -999f;
            if (pendingHeavyTime > 0f && TryFireHeavy()) pendingHeavyTime = -999f;
        }

        private bool TryFireLight()
        {
            if (animator == null) return false;
            if (Time.time - lastAcceptedTime < inputAcceptedLockout) return false;

            bool isAttacking = IsCurrentlyAttacking();
            bool comboReady = animator.GetBool(ComboReadyHash);

            if (isAttacking && !comboReady) return false;

            float cost = ChooseLightCost(comboReady);
            if (!stamina.TrySpend(cost)) return false;

            animator.SetTrigger(LightAttackHash);
            lastAcceptedTime = Time.time;
            return true;
        }

        private bool TryFireHeavy()
        {
            if (animator == null) return false;
            if (Time.time - lastAcceptedTime < inputAcceptedLockout) return false;
            if (IsInCommittedAttack()) return false;
            if (!stamina.TrySpend(heavyCost)) return false;
            animator.SetTrigger(HeavyAttackHash);
            lastAcceptedTime = Time.time;
            return true;
        }

        private bool IsInCommittedAttack()
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Heavy") || info.IsName("Light3")) return true;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Heavy") || next.IsName("Light3")) return true;
            }
            return false;
        }

        private bool IsCurrentlyAttacking()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).tagHash == AttackingTagHash) return true;
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).tagHash == AttackingTagHash) return true;
            return false;
        }

        private float ChooseLightCost(bool comboReady)
        {
            if (!comboReady) return light1Cost;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Light2")) return light3Cost;
            return light2Cost;
        }
    }
}
