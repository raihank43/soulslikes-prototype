using UnityEngine;

namespace Soulslike.Combat
{
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private Animator animator;

        private static readonly int ComboReadyHash = Animator.StringToHash("ComboReady");
        private static readonly int LungeSpeedHash = Animator.StringToHash("AttackLungeSpeed");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        public void EnableHitbox(int damage)
        {
            if (weaponHitbox != null) weaponHitbox.Enable(damage);
        }

        public void DisableHitbox()
        {
            if (weaponHitbox != null) weaponHitbox.Disable();
        }

        public void OpenComboWindow()
        {
            if (animator != null) animator.SetBool(ComboReadyHash, true);
        }

        public void CloseComboWindow()
        {
            if (animator != null) animator.SetBool(ComboReadyHash, false);
        }

        public void BeginLunge(float speed)
        {
            if (animator != null) animator.SetFloat(LungeSpeedHash, speed);
        }

        public void EndLunge()
        {
            if (animator != null) animator.SetFloat(LungeSpeedHash, 0f);
        }
    }
}
