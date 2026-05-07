// VRPointer.cs
// Runtime bootstrap for Quest 2 controller + hand tracking → UI interaction.
//
// Ray priority (highest to lowest):
//   1. Right controller (if tracked)
//   2. Left controller (if tracked)
//   3. Right hand (if hand tracking active — pinch = click)
//   4. Left hand (if hand tracking active — pinch = click)
//   5. Head-gaze fallback (look + trigger/tap)
//
// Click detection uses direct world-space ray-vs-RectTransform intersection.
// No dependency on GraphicRaycaster or screen-space projection.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace OphthalSuite.Core
{
    public class VRPointer : MonoBehaviour
    {
        private static VRPointer _instance;

        // ── State ────────────────────────────────────────────────────────────────
        private bool _initialised;
        private GameObject _pointerGo;
        private LineRenderer _line;
        private GameObject _reticle;        // kept for 3D laser only (not shown in overlay mode)
        private InputAction _triggerAction;
        private bool _triggerWasPressed;
        private XRInputDevice _rightController;
        private XRInputDevice _leftController;
        private XRInputDevice _rightHand;
        private XRInputDevice _leftHand;
        private float _nextInitAttemptTime;
        private float _nextControllerLogTime;
        private float _nextMissLogTime;
        private Selectable _hoveredSelectable;
        private float _hoverStartedAt;
        private bool _diagnosticsDumped;

        [Header("Laser Appearance")]
        [SerializeField] private Color laserColor = new Color(0.4f, 0.7f, 1f, 0.7f);
        [SerializeField] private float laserLength = 8f;

        [Header("Fallback Selection")]
        [SerializeField] private bool enableGazeDwellClick = true;
        [SerializeField] private float gazeDwellSeconds = 1.0f;

        // ── Unity lifecycle ──────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<VRPointer>() != null) return;
            var go = new GameObject("VRPointer_RuntimeBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<VRPointer>();
            Debug.Log("VRPointer: Runtime bootstrap created.");
        }

        private void Start()
        {
            _instance = this;
            Invoke(nameof(TryInit), 0.5f);
        }

        private void TryInit()
        {
            if (_initialised) return;
            if (!XRSetup.IsVRActive && Camera.main == null)
            {
                Debug.Log("VRPointer: waiting for VR headset/camera.");
                return;
            }

            SetupInteractionManager();
            SetupPointerVisuals();
            SetupReticle();
            SetupEventSystem();
            SetupInputActions();
            _initialised = true;
            Debug.Log("VRPointer: ✓ Pointer active. Controller → Hand → Head-gaze priority.");
        }

        private void LateUpdate()
        {
            if (!_initialised)
            {
                if (Time.unscaledTime >= _nextInitAttemptTime)
                {
                    _nextInitAttemptTime = Time.unscaledTime + 0.5f;
                    TryInit();
                }
                return;
            }

            if (_pointerGo == null || _line == null) return;

            // ── Primary: use TrackedPoseDriver's world-space transform ───────
            // TPD is already converting tracking-space → world-space correctly.
            // Only fall back to XRInputDevices / head-gaze if TPD reports origin.
            Vector3 origin;
            Vector3 forward;
            string source;

            Vector3 tpdPos = _pointerGo.transform.position;
            Vector3 tpdFwd = _pointerGo.transform.forward;
            bool tpdValid = tpdPos.sqrMagnitude > 0.001f; // TPD has moved from origin

            if (tpdValid)
            {
                origin = tpdPos;
                forward = tpdFwd;
                source = "controller_tpd";
            }
            else
            {
                // Fallback: try XRInputDevices, then head-gaze
                GetActiveRay(out origin, out forward, out source);
            }

            // Update laser visual — stop at canvas hit point if found
            Vector3 laserEnd = GetLaserEndPoint(origin, forward);
            _line.SetPosition(0, origin);
            _line.SetPosition(1, laserEnd);

            // Hover and click use the ACTIVE ray. Reticle visuals are disabled.
            UpdateHover(origin, forward);
            HandleTriggerClick(origin, forward);
        }

        public static bool TryGetPointerScreenPosition(out Vector2 screenPosition, out bool pressed)
        {
            screenPosition = default;
            pressed = false;

            var pointer = _instance != null ? _instance : FindFirstObjectByType<VRPointer>();
            if (pointer == null || !pointer._initialised)
                return false;

            pointer.ResolveActiveRay(out Vector3 origin, out Vector3 forward);
            pressed = pointer.IsClickPressed();
            if (!TryGetNearestCanvasHit(origin, forward, out Vector3 hitWorld, out _))
                return false;

            var cam = Camera.main;
            if (cam == null)
                return false;

            Vector3 screenPt = cam.WorldToScreenPoint(hitWorld);
            if (screenPt.z <= 0f)
                return false;

            screenPosition = new Vector2(screenPt.x, screenPt.y);
            return true;
        }

        public static bool TryGetPointerRay(out Vector3 origin, out Vector3 forward, out bool pressed)
        {
            origin = default;
            forward = Vector3.forward;
            pressed = false;

            var pointer = _instance != null ? _instance : FindFirstObjectByType<VRPointer>();
            if (pointer == null || !pointer._initialised)
                return false;

            pointer.ResolveActiveRay(out origin, out forward);
            forward = forward.normalized;
            pressed = pointer.IsClickPressed();
            return true;
        }

        private void ResolveActiveRay(out Vector3 origin, out Vector3 forward)
        {
            Vector3 tpdPos = _pointerGo != null ? _pointerGo.transform.position : Vector3.zero;
            Vector3 tpdFwd = _pointerGo != null ? _pointerGo.transform.forward : Vector3.forward;
            bool tpdValid = tpdPos.sqrMagnitude > 0.001f;

            if (tpdValid)
            {
                origin = tpdPos;
                forward = tpdFwd;
                return;
            }

            GetActiveRay(out origin, out forward, out _);
        }

        // ── Active Ray Resolution ────────────────────────────────────────────────

        private void GetActiveRay(out Vector3 origin, out Vector3 forward, out string source)
        {
            // 1. Right controller
            if (TryGetDevicePose(XRNode.RightHand, ref _rightController, out origin, out forward))
            {
                source = "right_controller";
                LogControllerState(true, source);
                return;
            }

            // 2. Left controller
            if (TryGetDevicePose(XRNode.LeftHand, ref _leftController, out origin, out forward))
            {
                source = "left_controller";
                LogControllerState(true, source);
                return;
            }

            // 3. Right hand (hand tracking)
            if (TryGetHandPose(XRNode.RightHand, ref _rightHand, out origin, out forward))
            {
                source = "right_hand";
                LogControllerState(true, source);
                return;
            }

            // 4. Left hand (hand tracking)
            if (TryGetHandPose(XRNode.LeftHand, ref _leftHand, out origin, out forward))
            {
                source = "left_hand";
                LogControllerState(true, source);
                return;
            }

            // 5. Head-gaze fallback
            GetHeadRay(out origin, out forward);
            source = "head_gaze";
            LogControllerState(false, source);
        }

        private bool TryGetDevicePose(XRNode node, ref XRInputDevice cached,
            out Vector3 pos, out Vector3 fwd)
        {
            if (!cached.isValid)
                cached = XRInputDevices.GetDeviceAtXRNode(node);

            if (cached.isValid &&
                cached.TryGetFeatureValue(XRCommonUsages.devicePosition, out pos) &&
                cached.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rot))
            {
                // Verify it's actually a controller (has trigger)
                if (cached.TryGetFeatureValue(XRCommonUsages.trigger, out float _tv))
                {
                    fwd = rot * Vector3.forward;
                    return true;
                }
            }

            pos = Vector3.zero;
            fwd = Vector3.forward;
            return false;
        }

        private bool TryGetHandPose(XRNode node, ref XRInputDevice cached,
            out Vector3 pos, out Vector3 fwd)
        {
            // Hand tracking devices appear on the same XR nodes but without trigger
            if (!cached.isValid)
            {
                var devices = new List<XRInputDevice>();
                XRInputDevices.GetDevicesAtXRNode(node, devices);
                foreach (var d in devices)
                {
                    // Hand tracking device won't have a trigger axis
                    if (d.isValid && !d.TryGetFeatureValue(XRCommonUsages.trigger, out float _))
                    {
                        if (d.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 _p))
                        {
                            cached = d;
                            break;
                        }
                    }
                }
            }

            if (cached.isValid &&
                cached.TryGetFeatureValue(XRCommonUsages.devicePosition, out pos) &&
                cached.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rot))
            {
                fwd = rot * Vector3.forward;
                return true;
            }

            pos = Vector3.zero;
            fwd = Vector3.forward;
            return false;
        }

        private static void GetHeadRay(out Vector3 origin, out Vector3 forward)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                origin = cam.transform.position + cam.transform.forward * 0.15f;
                forward = cam.transform.forward;
            }
            else
            {
                origin = Vector3.zero;
                forward = Vector3.forward;
            }
        }

        private void LogControllerState(bool tracked, string source)
        {
            if (Time.unscaledTime < _nextControllerLogTime) return;
            _nextControllerLogTime = Time.unscaledTime + 5f;
            Debug.Log($"VRPointer: active ray = {source}");
        }

        // ── Setup ────────────────────────────────────────────────────────────────

        private void SetupInteractionManager()
        {
            if (FindFirstObjectByType<XRInteractionManager>() != null) return;
            var go = new GameObject("XRInteractionManager");
            go.AddComponent<XRInteractionManager>();
            Debug.Log("VRPointer: Created XRInteractionManager.");
        }

        private void SetupPointerVisuals()
        {
            _pointerGo = new GameObject("VR_Pointer");
            DontDestroyOnLoad(_pointerGo);

            // TrackedPoseDriver for right hand controller
            var tpd = _pointerGo.AddComponent<TrackedPoseDriver>();
            var posAct = new InputAction("Pos", InputActionType.Value,
                "<XRController>{RightHand}/devicePosition");
            var rotAct = new InputAction("Rot", InputActionType.Value,
                "<XRController>{RightHand}/deviceRotation");
            posAct.Enable();
            rotAct.Enable();
            tpd.positionAction = posAct;
            tpd.rotationAction = rotAct;
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            // XRRayInteractor for XRI-based interaction
            var ray = _pointerGo.AddComponent<XRRayInteractor>();
            ray.maxRaycastDistance = laserLength;

            // Line renderer — bright white-cyan laser visible in VR
            _line = _pointerGo.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.startWidth = 0.012f;
            _line.endWidth = 0.004f;
            // Use Unlit shader so the laser renders clearly in VR regardless of lighting.
            // renderQueue 5000 puts it above all canvas layers (typically 3000-4000).
            Shader laserShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            var laserMat = new Material(laserShader);
            laserMat.color = Color.white;
            laserMat.renderQueue = 5000;
            // ZTest Always = laser always draws on top, depth buffer never hides it
            laserMat.SetInt("_ZWrite", 0);
            laserMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _line.material = laserMat;
            _line.startColor = new Color(1f, 1f, 1f, 1f);
            _line.endColor   = new Color(0.5f, 0.9f, 1f, 0.6f);
            _line.useWorldSpace = true;
            _line.sortingOrder = 5000;
            _line.receiveShadows = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetupReticle()
        {
            // Reticle visuals are disabled by request; keep this method for setup flow clarity.
        }

        private void SetupEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            // Remove conflicting input modules
            var sim = es.GetComponent<StandaloneInputModule>();
            if (sim != null) Destroy(sim);

            if (es.GetComponent<XRUIInputModule>() == null)
            {
                es.gameObject.AddComponent<XRUIInputModule>();
                Debug.Log("VRPointer: Added XRUIInputModule.");
            }
        }

        private void SetupInputActions()
        {
            _triggerAction = new InputAction("PointerClick", InputActionType.Button);
            // Right hand
            _triggerAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _triggerAction.AddBinding("<OculusTouchController>{RightHand}/triggerPressed");
            _triggerAction.AddBinding("<XRController>{RightHand}/primaryButton");
            _triggerAction.AddBinding("<XRController>{RightHand}/gripPressed");
            // Left hand
            _triggerAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _triggerAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            _triggerAction.AddBinding("<XRController>{LeftHand}/gripPressed");
            // Hand tracking pinch (Meta hand interaction profile)
            _triggerAction.AddBinding("<MetaAimHand>{RightHand}/indexPressed");
            _triggerAction.AddBinding("<MetaAimHand>{LeftHand}/indexPressed");
            _triggerAction.Enable();
        }

        // ── Laser end-point ──────────────────────────────────────────────────────

        /// <summary>Returns world-space endpoint of the laser — stops at first canvas hit.</summary>
        private Vector3 GetLaserEndPoint(Vector3 origin, Vector3 direction)
        {
            if (TryGetNearestCanvasHit(origin, direction, out Vector3 hitWorld, out _))
                return hitWorld;

            return origin + direction.normalized * laserLength;
        }

        // ── Reticle ──────────────────────────────────────────────────────────────

        private void UpdateReticlePosition(Vector3 origin, Vector3 direction)
        {
            // Visual reticle disabled by request. Laser and click handling remain active.
        }

        // ── Click Handling ───────────────────────────────────────────────────────

        private void HandleTriggerClick(Vector3 origin, Vector3 dir)
        {
            bool pressed = IsClickPressed();
            if (!pressed)
            {
                _triggerWasPressed = false;
                return;
            }

            if (_triggerWasPressed) return; // one-shot per press
            _triggerWasPressed = true;

            // Diagnostics on first click
            if (!_diagnosticsDumped)
            {
                _diagnosticsDumped = true;
                DumpDiagnostics(origin, dir);
            }

            // Try ray-vs-RectTransform intersection
            if (TryFindSelectableByRay(origin, dir, out Selectable hit, out _))
            {
                if (ClickSelectable(hit))
                {
                    Debug.Log($"VRPointer: ✓ clicked '{hit.name}'.");
                    AudioFeedbackManager.Instance?.PlayClick();
                    return;
                }
            }

            // Physics raycast fallback for emergency collider buttons
            if (TryPhysicsClick(origin, dir))
            {
                AudioFeedbackManager.Instance?.PlayClick();
                return;
            }

            LogClickMiss(origin, dir);
        }

        private bool IsClickPressed()
        {
            if (_triggerAction != null && _triggerAction.IsPressed())
                return true;

            return IsDeviceClickPressed(_rightController)
                || IsDeviceClickPressed(_leftController)
                || IsHandPinching(_rightHand)
                || IsHandPinching(_leftHand);
        }

        private static bool IsDeviceClickPressed(XRInputDevice device)
        {
            if (!device.isValid) return false;

            if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool trigger) && trigger)
                return true;
            if (device.TryGetFeatureValue(XRCommonUsages.trigger, out float tv) && tv > 0.55f)
                return true;
            if (device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primary) && primary)
                return true;
            if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool grip) && grip)
                return true;
            if (device.TryGetFeatureValue(XRCommonUsages.grip, out float gv) && gv > 0.55f)
                return true;

            return false;
        }

        private static bool IsHandPinching(XRInputDevice hand)
        {
            if (!hand.isValid) return false;
            // Hand tracking: triggerButton maps to index pinch on Oculus
            if (hand.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool pinch) && pinch)
                return true;
            return false;
        }

        // ── Ray-vs-Canvas hit, then nearest button centre ────────────────────────

        private bool TryFindSelectableByRay(Vector3 origin, Vector3 direction,
            out Selectable bestSelectable, out float bestDistance)
        {
            bestSelectable = null;
            bestDistance = float.PositiveInfinity;

            var es = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
            if (es == null) return false;

            Camera cam = Camera.main;
            if (cam == null) return false;

            if (!TryGetNearestCanvasHit(origin, direction, out Vector3 hitWorld, out bestDistance))
                return false;

            Vector3 screenPt = cam.WorldToScreenPoint(hitWorld);

            if (screenPt.z <= 0) return false; // behind camera

            var pointerData = new PointerEventData(es) { position = new Vector2(screenPt.x, screenPt.y) };

            var results = new List<RaycastResult>();
            es.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject == null) continue;
                var sel = result.gameObject.GetComponent<Selectable>()
                       ?? result.gameObject.GetComponentInParent<Selectable>();
                if (sel != null && sel.IsActive() && sel.IsInteractable())
                {
                    bestSelectable = sel;
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetNearestCanvasHit(Vector3 origin, Vector3 direction,
            out Vector3 hitWorld, out float bestDistance)
        {
            hitWorld = default;
            bestDistance = float.PositiveInfinity;

            var ray = new Ray(origin, direction.normalized);
            foreach (var canvas in FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                if (canvas.name.Contains("Reticle"))
                    continue;

                var rt = canvas.GetComponent<RectTransform>();
                if (rt == null)
                    continue;

                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out float distance) || distance <= 0f || distance >= bestDistance)
                    continue;

                Vector3 candidate = ray.GetPoint(distance);
                Vector3 local = canvas.transform.InverseTransformPoint(candidate);
                if (!rt.rect.Contains(new Vector2(local.x, local.y)))
                    continue;

                bestDistance = distance;
                hitWorld = candidate;
            }

            return bestDistance < float.PositiveInfinity;
        }

        // ── Physics raycast fallback ─────────────────────────────────────────────

        private static bool TryPhysicsClick(Vector3 origin, Vector3 direction)
        {
            Ray ray = new Ray(origin, direction.normalized);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10f))
                return false;

            var handler = hit.collider.GetComponent<IVRClickHandler>();
            if (handler != null)
            {
                handler.OnVRClick();
                Debug.Log($"VRPointer: ✓ Physics click on '{hit.collider.name}'.");
                return true;
            }
            return false;
        }

        // ── Hover / Dwell ────────────────────────────────────────────────────────

        private void UpdateHover(Vector3 origin, Vector3 direction)
        {
            if (!TryFindSelectableByRay(origin, direction, out Selectable sel, out _))
            {
                SetHoveredSelectable(null);
                return;
            }

            SetHoveredSelectable(sel);

            if (!enableGazeDwellClick || gazeDwellSeconds <= 0f)
                return;

            if (Time.unscaledTime - _hoverStartedAt >= gazeDwellSeconds)
            {
                if (ClickSelectable(sel))
                {
                    Debug.Log($"VRPointer: ✓ dwell-clicked '{sel.name}'.");
                    AudioFeedbackManager.Instance?.PlayClick();
                    _hoverStartedAt = Time.unscaledTime + 999f;
                }
            }
        }

        private void SetHoveredSelectable(Selectable selectable)
        {
            if (_hoveredSelectable == selectable)
                return;

            _hoveredSelectable = selectable;
            _hoverStartedAt = Time.unscaledTime;

            var es = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
            if (es != null)
                es.SetSelectedGameObject(
                    selectable != null ? selectable.gameObject : null);
        }

        // ── Click execution ──────────────────────────────────────────────────────

        private static bool ClickSelectable(Selectable selectable)
        {
            if (selectable == null || !selectable.IsInteractable())
                return false;

            var button = selectable as Button ?? selectable.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return true;
            }

            var toggle = selectable as Toggle ?? selectable.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
                return true;
            }

            var input = selectable as InputField ?? selectable.GetComponent<InputField>();
            if (input != null)
            {
                input.ActivateInputField();
                return true;
            }

            // Dropdown support
            var dropdown = selectable as Dropdown ?? selectable.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                dropdown.Show();
                return true;
            }

            return false;
        }

        // ── Diagnostics ──────────────────────────────────────────────────────────

        private void LogClickMiss(Vector3 origin, Vector3 direction)
        {
            if (Time.unscaledTime < _nextMissLogTime)
                return;
            _nextMissLogTime = Time.unscaledTime + 1f;

            int selCount = FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            int canCount = FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            Debug.LogWarning($"VRPointer: click miss. selectables={selCount}, " +
                $"canvases={canCount}, origin={origin:F2}, dir={direction:F2}");
        }

        private void DumpDiagnostics(Vector3 origin, Vector3 direction)
        {
            Debug.Log("VRPointer: ═══ DIAGNOSTICS (first click) ═══");

            foreach (var canvas in FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Debug.Log($"VRPointer: [DIAG] Canvas: '{canvas.name}', " +
                    $"mode={canvas.renderMode}, " +
                    $"worldCamera={canvas.worldCamera?.name ?? "NULL"}, " +
                    $"pos={canvas.transform.position:F2}");
            }

            Ray ray = new Ray(origin, direction.normalized);
            foreach (var sel in FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (sel == null) continue;
                var rt = sel.transform as RectTransform
                    ?? sel.GetComponent<RectTransform>();
                string hitInfo = "no-rt";
                if (rt != null)
                {
                    var plane = new Plane(rt.forward, rt.position);
                    if (plane.Raycast(ray, out float enter))
                    {
                        Vector3 hl = rt.InverseTransformPoint(ray.GetPoint(enter));
                        bool inB = rt.rect.Contains(new Vector2(hl.x, hl.y));
                        hitInfo = $"dist={enter:F2}, inBounds={inB}";
                    }
                    else hitInfo = "ray-miss-plane";
                }
                Debug.Log($"VRPointer: [DIAG] Selectable: '{sel.name}' " +
                    $"({sel.GetType().Name}), interactable={sel.IsInteractable()}, " +
                    $"pos={sel.transform.position:F2}, {hitInfo}");
            }

            Debug.Log("VRPointer: ═══ END DIAGNOSTICS ═══");
        }

        // ── Plane raycast helper (both sides) ────────────────────────────────────

        /// <summary>
        /// Raycast against a plane trying both normal directions.
        /// Returns positive enter distance if hit, or -1 if no hit.
        /// This handles canvases whose forward may point toward OR away from the user.
        /// </summary>
        private static float RaycastPlaneBothSides(Ray ray, Vector3 planeNormal, Vector3 planePoint)
        {
            // Try front face
            var plane = new Plane(planeNormal, planePoint);
            if (plane.Raycast(ray, out float enter) && enter > 0f)
                return enter;

            // Try back face (flipped normal)
            var flipped = new Plane(-planeNormal, planePoint);
            if (flipped.Raycast(ray, out enter) && enter > 0f)
                return enter;

            return -1f;
        }

        // ── Public: upgrade canvas for VR ────────────────────────────────────────

        public static void UpgradeCanvasForVR(GameObject canvasGo)
        {
            // Canvases are now ScreenSpaceOverlay — no TrackedDeviceGraphicRaycaster needed.
            // Standard GraphicRaycaster + EventSystem handles all interaction.
            // Method kept for call-site compatibility only.
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            _triggerAction?.Disable();
            _triggerAction?.Dispose();
            if (_pointerGo != null) Destroy(_pointerGo);
            if (_reticle != null) Destroy(_reticle);
        }
    }

    /// <summary>
    /// Implement on any MonoBehaviour with a Collider to receive VR clicks
    /// via Physics raycast (emergency fallback buttons).
    /// </summary>
    public interface IVRClickHandler
    {
        void OnVRClick();
    }
}
