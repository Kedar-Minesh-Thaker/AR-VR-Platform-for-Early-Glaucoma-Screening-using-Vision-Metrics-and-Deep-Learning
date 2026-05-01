// PatientInputHub.cs
// Central tap/mouse capture for the patient device. Tests can subscribe; existing
// Perimetry PatientResponse may remain unchanged — wire this hub in parallel when needed.

using System;
using UnityEngine;

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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        private static bool TryGetPress(out PatientInputEvent ev)
        {
            ev = default;

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
