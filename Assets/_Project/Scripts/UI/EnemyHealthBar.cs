using UnityEngine;
using UnityEngine.UI;
using Soulslike.Combat;

namespace Soulslike.UI
{
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Slider slider;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float viewportMargin = 0.05f;

        private Transform follow;
        private Camera cam;

        private void OnEnable()
        {
            if (health != null) health.HealthChanged += OnHealthChanged;
            if (health != null) OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();
            if (slider == null) slider = GetComponentInChildren<Slider>();
            follow = health != null ? health.transform : transform.parent;
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (follow == null || cam == null) return;

            Vector3 desired = follow.position + worldOffset;
            Vector3 vp = cam.WorldToViewportPoint(desired);

            if (vp.z > 0f)
            {
                vp.x = Mathf.Clamp(vp.x, viewportMargin, 1f - viewportMargin);
                vp.y = Mathf.Clamp(vp.y, viewportMargin, 1f - viewportMargin);
                desired = cam.ViewportToWorldPoint(vp);
            }

            transform.position = desired;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }

        private void OnHealthChanged(int current, int max)
        {
            if (slider == null) return;
            slider.maxValue = max;
            slider.value = current;
        }
    }
}
