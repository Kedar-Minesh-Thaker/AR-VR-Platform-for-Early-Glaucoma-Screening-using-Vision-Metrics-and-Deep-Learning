// PatientInputEvent.cs
// Shared patient-screen input envelope (Android touch, editor mouse, XR controllers).

using System;
using UnityEngine;

namespace OphthalSuite.Core.Input
{
    public enum PatientInputKind
    {
        Tap = 0,
        MouseButton = 1,
        Cancel = 2,
        Controller = 3
    }

    [Serializable]
    public struct PatientInputEvent
    {
        public PatientInputKind kind;
        public Vector2 screenPosition;
        public int fingerId;
        public float unscaledTime;
        public string source;
    }
}
