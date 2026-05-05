using UnityEngine;
using UnityEngine.UI;
using Soulslike.Player;

namespace Soulslike.UI
{
    [RequireComponent(typeof(Slider))]
    public class StaminaBar : MonoBehaviour
    {
        [SerializeField] private PlayerStamina stamina;
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.minValue = 0f;
            slider.interactable = false;
        }

        private void OnEnable()
        {
            if (stamina != null)
            {
                stamina.StaminaChanged += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (stamina != null) stamina.StaminaChanged -= Refresh;
        }

        private void Refresh()
        {
            slider.maxValue = stamina.Max;
            slider.value = stamina.Current;
        }
    }
}
