// AppFlowCoordinator.cs
// Scene flow + bridges doctor commands and local menu to AppOrchestrator / VisualTestManager.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OphthalSuite.Core
{
    public class AppFlowCoordinator : MonoBehaviour
    {
        public static AppFlowCoordinator Instance { get; private set; }

        [Header("Core")]
        [SerializeField] private AppOrchestrator orchestrator;
        [SerializeField] private VisualTestManager visualTestManager;

        [Header("Scenes (Build Settings)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string patientExamSceneName = "PatientExam";

        [Header("Defaults when doctor omits fields")]
        [SerializeField] private string defaultPatientId = "P001";
        [SerializeField] private string defaultEye = "OD";
        [SerializeField] private int defaultAge = 55;

        public bool IsExamSceneLoaded { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (visualTestManager == null) visualTestManager = FindFirstObjectByType<VisualTestManager>();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IsExamSceneLoaded = scene.name == patientExamSceneName;
            if (IsExamSceneLoaded)
            {
                if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
                if (visualTestManager == null) visualTestManager = FindFirstObjectByType<VisualTestManager>();
            }
        }

        public string MenuDefaultPatientId => defaultPatientId;
        public string MenuDefaultEye => defaultEye;
        public int MenuDefaultAge => defaultAge;

        public void LoadMainMenu(LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (orchestrator != null && orchestrator.IsSessionRunning)
                orchestrator.EndSession();
            if (!string.IsNullOrEmpty(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName, mode);
        }

        public void LoadPatientExam(LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (!string.IsNullOrEmpty(patientExamSceneName))
                SceneManager.LoadScene(patientExamSceneName, mode);
        }

        public void LoadPatientExamAdditive()
        {
            if (!string.IsNullOrEmpty(patientExamSceneName))
                SceneManager.LoadScene(patientExamSceneName, LoadSceneMode.Additive);
        }

        /// <summary>Local / menu: start one test after loading exam scene if needed.</summary>
        public void StartSingleTest(string patientId, string eye, int age, string testId, bool loadExamScene = true)
        {
            if (string.IsNullOrEmpty(testId)) return;
            if (loadExamScene && !string.IsNullOrEmpty(patientExamSceneName))
            {
                _pendingSingle = (patientId, eye, age, testId);
                LoadPatientExam();
                return;
            }
            RunSingle(patientId, eye, age, testId);
        }

        private (string p, string e, int a, string t)? _pendingSingle;

        private void Update()
        {
            if (_pendingSingle == null) return;
            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (orchestrator == null) return;
            var x = _pendingSingle.Value;
            _pendingSingle = null;
            RunSingle(x.p, x.e, x.a, x.t);
        }

        private void RunSingle(string patientId, string eye, int age, string testId)
        {
            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (orchestrator == null) return;
            if (visualTestManager == null) visualTestManager = FindFirstObjectByType<VisualTestManager>();
            if (visualTestManager != null)
                visualTestManager.LoadTest(testId);
            orchestrator.StartSession(patientId, eye, age, new[] { testId });
        }

        public void StartFullBattery(string patientId, string eye, int age)
        {
            if (visualTestManager == null) visualTestManager = FindFirstObjectByType<VisualTestManager>();
            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (visualTestManager != null && orchestrator != null)
            {
                var first = visualTestManager.GetModulesInOrder();
                if (first.Count > 0)
                    visualTestManager.LoadTest(first[0].TestId);
            }
            orchestrator?.StartSession(patientId, eye, age, null);
        }

        public void StartOrderedList(string patientId, string eye, int age, IList<string> ids)
        {
            if (ids == null || ids.Count == 0) return;
            if (visualTestManager == null) visualTestManager = FindFirstObjectByType<VisualTestManager>();
            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            if (visualTestManager != null)
                visualTestManager.LoadTest(ids[0]);
            orchestrator?.StartSession(patientId, eye, age, ids);
        }

        public void EndSessionFromDoctor()
        {
            if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
            orchestrator?.EndSession();
        }

        public void HandleDoctorCommand(DoctorCommandEnvelope cmd)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.command)) return;

            string pid = string.IsNullOrEmpty(cmd.patientId) ? defaultPatientId : cmd.patientId;
            string eye = string.IsNullOrEmpty(cmd.eye) ? defaultEye : cmd.eye;
            int age = cmd.age > 0 ? cmd.age : defaultAge;
            string cid = string.IsNullOrEmpty(cmd.commandId) ? Guid.NewGuid().ToString("N") : cmd.commandId;

            bool ok = true;
            string detail = "";

            try
            {
                switch (cmd.command.ToUpperInvariant())
                {
                    case "RETURN_MENU":
                    case "GO_MENU":
                        LoadMainMenu();
                        break;
                    case "LOAD_EXAM":
                        LoadPatientExam();
                        break;
                    case "END_SESSION":
                        EndSessionFromDoctor();
                        break;
                    case "START_SINGLE_TEST":
                        if (string.IsNullOrEmpty(cmd.testId)) throw new Exception("testId required");
                        StartSingleTest(pid, eye, age, cmd.testId, loadExamScene: true);
                        break;
                    case "START_SINGLE_TEST_HERE":
                        if (string.IsNullOrEmpty(cmd.testId)) throw new Exception("testId required");
                        RunSingle(pid, eye, age, cmd.testId);
                        break;
                    case "START_FULL_BATTERY":
                        if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
                        LoadPatientExam();
                        _pendingFull = (pid, eye, age);
                        break;
                    case "START_ORDERED":
                        var list = ParseIds(cmd.testIdsOrdered);
                        if (list.Count == 0) throw new Exception("testIdsOrdered empty");
                        LoadPatientExam();
                        _pendingOrdered = (pid, eye, age, list);
                        break;
                    default:
                        ok = false;
                        detail = "unknown command";
                        break;
                }
            }
            catch (Exception ex)
            {
                ok = false;
                detail = ex.Message;
            }

            var ack = new PatientCommandAck
            {
                commandId = cid,
                command = cmd.command,
                ok = ok,
                detail = detail,
                timestampUtc = DateTime.UtcNow.ToString("o")
            };
            SharedDoctorMirror.Instance?.Broadcast(ack);
        }

        private (string p, string e, int a)? _pendingFull;
        private (string p, string e, int a, List<string> ids)? _pendingOrdered;

        private void LateUpdate()
        {
            if (_pendingFull != null)
            {
                if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
                if (orchestrator != null)
                {
                    var x = _pendingFull.Value;
                    _pendingFull = null;
                    StartFullBattery(x.p, x.e, x.a);
                }
            }

            if (_pendingOrdered != null)
            {
                if (orchestrator == null) orchestrator = FindFirstObjectByType<AppOrchestrator>();
                if (orchestrator != null)
                {
                    var x = _pendingOrdered.Value;
                    _pendingOrdered = null;
                    StartOrderedList(x.p, x.e, x.a, x.ids);
                }
            }
        }

        private static List<string> ParseIds(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var part in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }
    }
}
