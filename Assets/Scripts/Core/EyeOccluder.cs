// EyeOccluder.cs
// In VR: blacks out the non-tested eye so the patient sees only through
// the eye being examined. Essential for monocular visual-field tests.
//
// Usage: call SetTestedEye("OD") or SetTestedEye("OS").
//   OD (right eye) = occlude left eye display
//   OS (left eye)  = occlude right eye display
//   "" or null      = both eyes visible (binocular)
//
// Quest 2 rendering: uses a URP-compatible full-screen blit approach that
// works with both Multi-Pass and Single-Pass Instanced stereo rendering.
// We use Camera.stereoActiveEye to determine which eye is being rendered.
//
// In non-VR mode: does nothing (the physical eye-patch is used instead).

using UnityEngine;
using UnityEngine.XR;

namespace OphthalSuite.Core
{
    [RequireComponent(typeof(Camera))]
    public class EyeOccluder : MonoBehaviour
    {
        private Camera _cam;
        private string _testedEye = "";           // "OD" or "OS"
        private Material _blackMat;
        private bool _occluding;

        // Which stereo eye to block (opposite of tested eye)
        private Camera.MonoOrStereoscopicEye _blockedEye;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            // Use an Unlit/Color shader — compatible with both URP and built-in pipeline
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                // Fallback: try a URP-compatible shader
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                Debug.LogWarning("EyeOccluder: no compatible shader found; eye occlusion disabled.");
                enabled = false;
                return;
            }
            _blackMat = new Material(shader);
            _blackMat.color = Color.black;
        }

        /// <summary>
        /// Set which eye is being tested. The OTHER eye gets blacked out.
        /// Pass null or "" for binocular (both visible).
        /// </summary>
        public void SetTestedEye(string eye)
        {
            _testedEye = (eye ?? "").ToUpper().Trim();

            if (!XRSetup.IsVRActive || string.IsNullOrEmpty(_testedEye))
            {
                _occluding = false;
                return;
            }

            // OD = right eye tested → block LEFT display
            // OS = left eye tested  → block RIGHT display
            if (_testedEye == "OD")
            {
                _blockedEye = Camera.MonoOrStereoscopicEye.Left;
                _occluding = true;
                Debug.Log("EyeOccluder: testing OD — occluding LEFT eye.");
            }
            else if (_testedEye == "OS")
            {
                _blockedEye = Camera.MonoOrStereoscopicEye.Right;
                _occluding = true;
                Debug.Log("EyeOccluder: testing OS — occluding RIGHT eye.");
            }
            else
            {
                _occluding = false;
            }
        }

        /// <summary>Clear occlusion (binocular mode).</summary>
        public void ClearOcclusion()
        {
            _occluding = false;
            _testedEye = "";
        }

        // OnRenderImage is called after the camera renders each eye.
        // In Single-Pass Instanced: Unity calls this per-eye with the correct
        // stereoActiveEye value, so the occlusion still works correctly.
        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (!_occluding || !XRSetup.IsVRActive)
            {
                Graphics.Blit(src, dest);
                return;
            }

            // Check which stereo eye is currently being rendered
            var currentEye = _cam.stereoActiveEye;
            if (currentEye == _blockedEye)
            {
                // Render solid black to the occluded eye
                Graphics.Blit(Texture2D.blackTexture, dest, _blackMat);
            }
            else
            {
                // Pass through normally to the tested eye
                Graphics.Blit(src, dest);
            }
        }

        private void OnDestroy()
        {
            if (_blackMat != null) Destroy(_blackMat);
        }
    }
}
