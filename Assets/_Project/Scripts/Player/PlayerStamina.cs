using System;
using UnityEngine;

namespace Soulslike.Player
{
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenPerSecond = 20f;
        [SerializeField] private float regenDelay = 1f;

        public float Current { get; private set; }
        public float Max => maxStamina;
        public event Action StaminaChanged;

        private float lastSpendTime = -999f;

        private void Awake()
        {
            Current = maxStamina;
            StaminaChanged?.Invoke();
        }

        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            Current = Mathf.Max(0f, Current - amount);
            lastSpendTime = Time.time;
            StaminaChanged?.Invoke();
            return true;
        }

        private void Update()
        {
            if (Current >= maxStamina) return;
            if (Time.time - lastSpendTime < regenDelay) return;

            float prev = Current;
            Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(prev, Current)) StaminaChanged?.Invoke();
        }
    }
}
