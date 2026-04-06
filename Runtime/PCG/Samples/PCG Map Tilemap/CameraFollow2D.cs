using UnityEngine;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Minimal camera-follow script for the PCG Map Tilemap sample scene.
    ///
    /// Phase H7 — Map Navigation Sample.
    /// Attach to the Main Camera. Assign the player Transform as <see cref="target"/>.
    /// Camera tracks the target in LateUpdate with optional smoothing.
    /// Z position is preserved so the camera stays on its rendering plane.
    ///
    /// Pure sample-side. No Cinemachine dependency.
    /// </summary>
    [AddComponentMenu("Islands/PCG/Camera Follow 2D")]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Tooltip("The Transform to follow (typically the player).")]
        [SerializeField] private Transform target;

        [Tooltip("Smoothing time in seconds. 0 = instant snap. ~0.1 = responsive. ~0.3 = gentle.")]
        [Min(0f)]
        [SerializeField] private float smoothTime = 0.1f;

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
                : desired;
        }
    }
}