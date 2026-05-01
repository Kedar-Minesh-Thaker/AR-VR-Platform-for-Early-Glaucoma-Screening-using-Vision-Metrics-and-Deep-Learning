// PatientInputHub.cs
// Central tap/mouse/controller capture for the patient device. Tests can subscribe;
// existing Perimetry PatientResponse may remain unchanged — wire this hub in parallel
// when needed.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OphthalSuite.Core.Input
{
    public class PatientInputHub : MonoBehaviour
    {
        public static PatientInputHub Instance { get; private set; }

        [Tooltip("Minimum seconds between emitted events (debounce).")]
        [SerializeField] private float debounceSec = 0.15f;

        public event Action<PatientInputEvent> OnPatientInput;

        private float _lastEmit = -999f;
        private bool _listening = true;

        // ── XR Controller Input Actions ──────────────────────────────────────────
        private InputAction _triggerAction;
        private InputAction _buttonAction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Bind to Quest controller trigger (either hand)
            _triggerAction = new InputAction("HubTrigger", InputActionType.Button);
            _triggerAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _triggerAction.AddBinding("<XRController>{LeftHand}/triggerPressed");

            // Bind to A button (right) and X button (left)
            _buttonAction = new InputAction("HubButton", InputActionType.Button);
            _buttonAction.AddBinding("<XRController>{RightHand}/primaryButton");
            _buttonAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        }

        private void OnEnable()
        {
            _triggerAction?.Enable();
            _buttonAction?.Enable();
        }

        private void OnDisable()
        {
            _triggerAction?.Disable();
            _buttonAction?.Disable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _triggerAction?.Dispose();
            _buttonAction?.Dispose();
        }

        public void SetListening(bool enabled) => _listening = enabled;

        private void Update()
        {
            if (!_listening || OnPatientInput == null) return;

            if (TryGetPress(out PatientInputEvent ev))
            {
                if (Time.unscaledTime - _lastEmit < debounceSec) return;
                _lastEmit = Time.unscaledTime;
                OnPatientInput?.Invoke(ev);
            }
        }

        private bool TryGetPress(out PatientInputEvent ev)
        {
            ev = default;

            // Quest controller trigger or button
            if (_triggerAction != null && _triggerAction.WasPerformedThisFrame())
            {
                ev = new PatientInputEvent
                {
                    kind = PatientInputKind.Controller,
                    screenPosition = Vector2.zero,
                    fingerId = -1,
                    unscaledTime = Time.unscaledTime,
                    source = "xr_trigger"
                };
                return true;
            }

            if (_buttonAction != null && _buttonAction.WasPerformedThisFrame())
            {
                ev = new PatientInputEvent
                {
                    kind = PatientInputKind.Controller,
                    screenPosition = Vector2.zero,
                    fingerId = -1,
                    unscaledTime = Time.unscaledTime,
                    source = "xr_button"
                };
                return true;
            }

            // Mouse (editor testing)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                ev = new PatientInputEvent
                {
                    kind = PatientInputKind.MouseButton,
                    screenPosition = UnityEngine.Input.mousePosition,
                    fingerId = -1,
                    unscaledTime = Time.unscaledTime,
                    source = "mouse"
                };
                return true;
            }

            // Touch (phone fallback)
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                ev = new PatientInputEvent
                {
                    kind = PatientInputKind.Tap,
                    screenPosition = t.position,
                    fingerId = t.fingerId,
                    unscaledTime = Time.unscaledTime,
                    source = "touch"
                };
                return true;
            }

            return false;
        }
    }
}
