using System;
using UnityEngine;

namespace Soulslike.Combat
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0;

        public event Action<int, int> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Debug.Log($"{name} took {amount} dmg → {CurrentHealth}/{maxHealth}");
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (IsDead)
            {
                Debug.Log($"{name} died");
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                Died?.Invoke();
            }
        }
    }
}
