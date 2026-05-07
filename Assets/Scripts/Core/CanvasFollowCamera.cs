// CanvasFollowCamera.cs
// Keeps a world-space canvas in front of the VR headset.
//
// The canvas is NOT parented to the camera — it lives in world space and
// smoothly tracks the head position every frame. This keeps UI always at
// eye level while remaining stable enough for comfortable interaction.

using UnityEngine;

namespace OphthalSuite.Core
{
    public class CanvasFollowCamera : MonoBehaviour
    {
        private float _distance = 1.6f;
        private float _verticalOffset = 0f;

        // How quickly the canvas chases the ideal position.
        // 10 = snappy but smooth; increase for more locked-in feel.
        [SerializeField] private float followSpeed = 10f;

        public void Init(float distance, float verticalOffset)
        {
            _distance = distance;
            _verticalOffset = verticalOffset;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Ideal position: directly in front of the camera
            Vector3 target = cam.transform.position
                + cam.transform.forward * _distance
                + cam.transform.up * _verticalOffset;

            // Face the camera (use world up to keep canvas upright)
            Quaternion targetRot = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            // Smooth follow — no guards, no conditions
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
        }
    }
}
