using UnityEngine;
using UnityEngine.UI;
using Soulslike.Combat;

namespace Soulslike.UI
{
    [RequireComponent(typeof(Slider))]
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.minValue = 0f;
            slider.interactable = false;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                OnHealthChanged(health.CurrentHealth, health.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int current, int max)
        {
            slider.maxValue = max;
            slider.value = current;
        }
    }
}
