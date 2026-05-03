using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using Soulslike.Input;

namespace Soulslike.Combat
{
    public class LockOnSystem : MonoBehaviour
    {
        [Header("Acquisition")]
        [SerializeField] private float acquireRadius = 15f;
        [SerializeField] private float releaseRadius = 25f;
        [SerializeField] private LayerMask enemyMask;

        [Header("Cinemachine")]
        [SerializeField] private CinemachineVirtualCamera lockedCam;
        [SerializeField] private CinemachineFreeLook freeLook;
        [SerializeField] private int lockedCamPriorityActive = 20;
        [SerializeField] private int lockedCamPriorityInactive = 5;

        [Header("Switch target (right-stick flick)")]
        [SerializeField] private float switchFlickThreshold = 0.85f;
        [SerializeField] private float switchCooldown = 0.3f;

        public Transform CurrentTarget { get; private set; }
        public bool IsLocked => CurrentTarget != null;

        private PlayerControls controls;
        private Camera cam;
        private float lastSwitchTime;
        private bool flickArmed = true;

        private void Awake()
        {
            cam = Camera.main;
            controls = new PlayerControls();
            controls.Player.LockOn.performed += OnLockOnPressed;
            controls.Player.Sprint.performed += OnSprintPressed;
        }

        private void OnEnable() => controls.Player.Enable();
        private void OnDisable() => controls.Player.Disable();

        private void OnDestroy()
        {
            if (controls != null)
            {
                controls.Player.LockOn.performed -= OnLockOnPressed;
                controls.Player.Sprint.performed -= OnSprintPressed;
                controls.Dispose();
            }
        }

        private void OnSprintPressed(InputAction.CallbackContext ctx)
        {
            if (IsLocked) ClearTarget();
        }

        private void OnLockOnPressed(InputAction.CallbackContext ctx)
        {
            if (IsLocked)
            {
                ClearTarget();
                return;
            }
            var found = FindBestTarget();
            if (found != null) SetTarget(found);
        }

        private void SetTarget(Transform t)
        {
            CurrentTarget = t;
            if (lockedCam != null)
            {
                lockedCam.LookAt = t;
                lockedCam.Priority = lockedCamPriorityActive;
            }
        }

        private void ClearTarget()
        {
            CurrentTarget = null;
            if (lockedCam != null)
            {
                lockedCam.LookAt = null;
                lockedCam.Priority = lockedCamPriorityInactive;
            }
            SyncFreeLookToCurrentView();
        }

        private void SyncFreeLookToCurrentView()
        {
            if (freeLook == null || cam == null) return;
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            freeLook.m_XAxis.Value = yaw;
        }

        private void Update()
        {
            if (!IsLocked) return;

            var th = CurrentTarget.GetComponent<EnemyHealth>();
            if (th != null && th.IsDead)
            {
                ClearTarget();
                return;
            }

            if (Vector3.Distance(transform.position, CurrentTarget.position) > releaseRadius)
            {
                ClearTarget();
                return;
            }

            HandleSwitchFlick();
        }

        private void HandleSwitchFlick()
        {
            float lookX = controls.Player.Look.ReadValue<Vector2>().x;
            if (Mathf.Abs(lookX) < switchFlickThreshold)
            {
                flickArmed = true;
                return;
            }
            if (!flickArmed) return;
            if (Time.time - lastSwitchTime < switchCooldown) return;

            Debug.Log($"[LockOn] switch target {(lookX > 0 ? "right" : "left")} (stub)");
            flickArmed = false;
            lastSwitchTime = Time.time;
        }

        private Transform FindBestTarget()
        {
            if (cam == null) cam = Camera.main;
            var hits = Physics.OverlapSphere(transform.position, acquireRadius, enemyMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return null;

            Plane[] frustum = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

            Transform best = null;
            float bestScore = float.PositiveInfinity;
            Vector2 viewportCenter = new Vector2(0.5f, 0.5f);

            foreach (var col in hits)
            {
                var t = col.transform;
                if (t == transform) continue;

                var b = col.bounds;
                if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, b)) continue;

                Vector3 vp = cam != null ? cam.WorldToViewportPoint(b.center) : Vector3.zero;
                if (cam != null && vp.z < 0f) continue;

                float dist = Vector3.Distance(transform.position, t.position);
                float screenDist = Vector2.Distance(new Vector2(vp.x, vp.y), viewportCenter);
                float score = screenDist + dist * 0.02f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }
    }
}
