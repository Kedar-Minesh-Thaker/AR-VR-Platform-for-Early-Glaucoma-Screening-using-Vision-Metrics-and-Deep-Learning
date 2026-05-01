// DoctorCommandModels.cs
// Laptop → patient command envelope (JSON over WebSocket). Backend / audit friendly.

using System;
using UnityEngine;

namespace OphthalSuite.Core
{
    [Serializable]
    public class DoctorCommandEnvelope
    {
        public string messageType = "DOCTOR_COMMAND";
        public string command;
        public string patientId = "P001";
        public string eye = "OD";
        public int age = 55;
        public string testId;
        public string testIdsOrdered;
        public string issuedAtUtc;
        public string commandId;
    }

    [Serializable]
    public class PatientCommandAck
    {
        public string messageType = "PATIENT_COMMAND_ACK";
        public string commandId;
        public string command;
        public bool ok;
        public string detail;
        public string timestampUtc;
    }
}
