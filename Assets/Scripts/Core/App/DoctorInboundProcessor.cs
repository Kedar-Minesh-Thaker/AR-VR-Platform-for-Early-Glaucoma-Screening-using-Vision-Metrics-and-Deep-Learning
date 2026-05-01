// DoctorInboundProcessor.cs
// Drains inbound WebSocket JSON from SharedDoctorMirror on the main thread and routes doctor commands.

using UnityEngine;

namespace OphthalSuite.Core
{
    [DefaultExecutionOrder(-40)]
    public class DoctorInboundProcessor : MonoBehaviour
    {
        [SerializeField] [Tooltip("Max JSON messages processed per frame (avoid spikes).")]
        private int maxPerFrame = 32;

        private void Update()
        {
            var mirror = SharedDoctorMirror.Instance;
            if (mirror == null) return;

            int n = maxPerFrame;
            while (n-- > 0 && mirror.TryDequeueInboundJson(out string json))
            {
                if (string.IsNullOrEmpty(json)) continue;
                if (json.IndexOf("DOCTOR_COMMAND", System.StringComparison.Ordinal) < 0) continue;

                var cmd = JsonUtility.FromJson<DoctorCommandEnvelope>(json);
                if (cmd == null || string.IsNullOrEmpty(cmd.command)) continue;
                if (!string.IsNullOrEmpty(cmd.messageType) &&
                    cmd.messageType != "DOCTOR_COMMAND") continue;

                AppFlowCoordinator.Instance?.HandleDoctorCommand(cmd);
            }
        }
    }
}
