using UnityEngine;
using Soulslike.Combat;

namespace Soulslike.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class LockOnIndicator : MonoBehaviour
    {
        [SerializeField] private LockOnSystem lockOn;
        [SerializeField] private float scale = 0.4f;
        [SerializeField] private float towardCameraOffset = 0.6f;

        private SpriteRenderer sr;
        private Camera cam;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            cam = Camera.main;
            transform.localScale = Vector3.one * scale;
            sr.enabled = false;
        }

        private static Vector3 ResolveCenter(Transform t)
        {
            var col = t.GetComponentInChildren<Collider>();
            if (col != null) return col.bounds.center;
            var rend = t.GetComponentInChildren<Renderer>();
            if (rend != null) return rend.bounds.center;
            return t.position;
        }

        private void LateUpdate()
        {
            if (lockOn == null || !lockOn.IsLocked)
            {
                if (sr.enabled) sr.enabled = false;
                return;
            }

            if (!sr.enabled) sr.enabled = true;

            var t = lockOn.CurrentTarget;
            Vector3 center = ResolveCenter(t);

            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCenter = center - cam.transform.position;
                Vector3 awayFromCam = toCenter;
                awayFromCam.y = 0f;
                if (awayFromCam.sqrMagnitude > 0.0001f)
                {
                    Vector3 awayNorm = awayFromCam.normalized;
                    transform.position = center - awayNorm * towardCameraOffset;
                    transform.rotation = Quaternion.LookRotation(awayNorm, Vector3.up);
                }
                else
                {
                    transform.position = center;
                }
            }
            else
            {
                transform.position = center;
            }
        }
    }
}
