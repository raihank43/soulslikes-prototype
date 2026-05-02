using UnityEngine;

namespace Soulslike.Player
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 worldOffset = Vector3.zero;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }
            transform.position = target.position + worldOffset;
        }
    }
}
