using System.Collections.Generic;
using UnityEngine;

namespace Soulslike.Combat
{
    [RequireComponent(typeof(Collider))]
    public class WeaponHitbox : MonoBehaviour
    {
        private readonly HashSet<EnemyHealth> hitThisSwing = new HashSet<EnemyHealth>();
        private Collider col;
        private int currentDamage;

        private void Awake()
        {
            col = GetComponent<Collider>();
            col.isTrigger = true;
            col.enabled = false;
        }

        public void Enable(int damage)
        {
            currentDamage = damage;
            hitThisSwing.Clear();
            col.enabled = true;
        }

        public void Disable()
        {
            col.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<EnemyHealth>();
            if (health == null || health.IsDead) return;
            if (!hitThisSwing.Add(health)) return;
            health.TakeDamage(currentDamage);
        }
    }
}
