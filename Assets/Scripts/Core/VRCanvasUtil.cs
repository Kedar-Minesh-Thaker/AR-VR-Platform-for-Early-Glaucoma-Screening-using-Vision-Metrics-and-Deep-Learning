// VRCanvasUtil.cs
// Places world-space UI canvases in front of the VR headset by parenting
// them directly to the camera. This guarantees the canvas is always at
// eye level regardless of tracking origin mode.
//
// NOTE: We use VRPointer's custom ray-vs-RectTransform math which works
// correctly with camera-parented canvases. We no longer use GraphicRaycaster
// or TrackedDeviceGraphicRaycaster, so camera-parenting is safe.

using UnityEngine;
using UnityEngine.UI;

namespace OphthalSuite.Core
{
    public static class VRCanvasUtil
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float MonitorDistance = 2.8f;
        public const float MonitorScale = 0.00145f;

        /// <summary>
        /// Configure a Unity UI canvas as a fixed-size world-space monitor.
        /// Unlike ScreenSpaceCamera, distance and scale now actually affect
        /// apparent size in the headset.
        /// </summary>
        public static void ConfigureFloatingMonitorCanvas(
            Canvas canvas,
            GameObject canvasGo,
            int sortingOrder,
            float verticalOffset = 0f)
        {
            if (canvas == null || canvasGo == null) return;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = sortingOrder;

            PlaceInFrontOfCamera(canvasGo, ReferenceResolution, MonitorDistance, MonitorScale, verticalOffset);

            var scaler = canvasGo.GetComponent<CanvasScaler>() ?? canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
        }

        /// <summary>
        /// Attach a world-space canvas to the camera so it stays at eye level.
        /// The canvas is parented to Camera.main with a fixed local offset.
        /// </summary>
        public static void PlaceInFrontOfCamera(GameObject canvasGo, Vector2 size, float distance, float scale, float verticalOffset = 0f)
        {
            if (canvasGo == null) return;

            var rt = canvasGo.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = size;

            var cam = Camera.main;

            if (cam != null)
            {
                // Parent to camera — canvas will always be at eye level
                canvasGo.transform.SetParent(cam.transform, false);
                canvasGo.transform.localPosition = new Vector3(0f, verticalOffset, distance);
                canvasGo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Fallback: place in world space if no camera yet
                canvasGo.transform.SetParent(null);
                canvasGo.transform.position = new Vector3(0f, verticalOffset, distance);
                canvasGo.transform.rotation = Quaternion.identity;
            }

            canvasGo.transform.localScale = Vector3.one * scale;

            // Remove any stale CanvasFollowCamera (no longer needed)
            var follow = canvasGo.GetComponent<CanvasFollowCamera>();
            if (follow != null)
                Object.Destroy(follow);

            Debug.Log($"VRCanvasUtil: Parented '{canvasGo.name}' to camera at local z={distance}m.");
        }
    }
}
