using System.Collections.Generic;
using UnityEngine;
using Soulslike.Combat;

namespace Soulslike.AI
{
    [RequireComponent(typeof(Collider))]
    public class EnemyHitbox : MonoBehaviour
    {
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        [SerializeField] private LayerMask targetMask = ~0;

        private Collider col;
        private BoxCollider boxCol;
        private int currentDamage;
        private bool active;
        private readonly HashSet<PlayerHealth> hitThisSwing = new HashSet<PlayerHealth>();

        private void Awake()
        {
            col = GetComponent<Collider>();
            col.isTrigger = true;
            boxCol = col as BoxCollider;
        }

        public void Enable(int damage)
        {
            currentDamage = damage;
            hitThisSwing.Clear();
            active = true;
        }

        public void Disable()
        {
            active = false;
        }

        // Manual poll handles two cases that OnTrigger* miss:
        //   1) Player Rigidbody is sleeping — no OnTriggerStay events fire.
        //   2) Hitbox enabled while player already inside — no fresh OnTriggerEnter.
        private void FixedUpdate()
        {
            if (!active || boxCol == null) return;
            Vector3 worldCenter = boxCol.transform.TransformPoint(boxCol.center);
            Vector3 worldHalf = Vector3.Scale(boxCol.size, boxCol.transform.lossyScale) * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(worldCenter, worldHalf, OverlapBuffer, boxCol.transform.rotation, targetMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) TryHit(OverlapBuffer[i]);
        }

        private void TryHit(Collider other)
        {
            if (other == null) return;
            var hp = other.GetComponentInParent<PlayerHealth>();
            if (hp == null || hp.IsDead) return;
            if (hitThisSwing.Contains(hp)) return;
            hitThisSwing.Add(hp);
            hp.TakeDamage(currentDamage);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (active) TryHit(other);
        }
    }
}
