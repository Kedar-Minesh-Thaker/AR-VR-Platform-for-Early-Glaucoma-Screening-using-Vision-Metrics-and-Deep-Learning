// SessionStartUI.cs
// Runtime UI to start/stop sessions.  Works in both VR and flat-screen mode.
//
// Features:
//   - Test selector dropdown (run specific tests or all)
//   - Quick Demo toggle (reduces perimetry to 10 loci for fast testing)
//   - Skip button to jump to next test or end session
//   - VR: world-space Canvas at 2m ahead
//   - Phone: screen-space overlay

using UnityEngine;
using UnityEngine.UI;
using OphthalSuite.Core;
using System.Collections.Generic;

namespace OphthalSuite.Core
{
    public class SessionStartUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Defaults")]
        [SerializeField] private string defaultPatientId = "P001";
        [SerializeField] private int    defaultAge       = 55;
        [SerializeField] private Font   uiFont;

        // ── State ────────────────────────────────────────────────────────────────
        private AppOrchestrator _orchestrator;
        private Canvas   _canvas;
        private InputField _patientField;
        private InputField _ageField;
        private Toggle   _odToggle;
        private Toggle   _osToggle;
        private Dropdown _testDropdown;
        private Dropdown _langDropdown;
        private Toggle   _demoToggle;
        private Button   _startBtn;
        private Button   _stopBtn;
        private Button   _skipBtn;
        private Text     _statusText;
        private Text     _progressText;
        private GameObject _startPanel;
        private float    _statusTimer;

        // Test IDs for dropdown
        private readonly List<string> _testIds = new List<string>();

        private void Awake()
        {
            _orchestrator = GetComponent<AppOrchestrator>()
                            ?? FindFirstObjectByType<AppOrchestrator>();
        }

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f && _statusText != null)
                    _statusText.text = "";
            }

            // Auto-hide start panel if session started externally
            if (_orchestrator != null && _orchestrator.IsSessionRunning && _startPanel.activeSelf)
            {
                _startPanel.SetActive(false);
                _stopBtn.gameObject.SetActive(true);
                _skipBtn.gameObject.SetActive(true);
            }

            // Show session back if ended externally
            if (_orchestrator != null && !_orchestrator.IsSessionRunning && !_startPanel.activeSelf
                && _stopBtn.gameObject.activeSelf)
            {
                _stopBtn.gameObject.SetActive(false);
                _skipBtn.gameObject.SetActive(false);
                _startPanel.SetActive(true);
            }

            // Update progress text
            if (_orchestrator != null && _orchestrator.IsSessionRunning && _progressText != null)
            {
                var cur = _orchestrator.CurrentTest;
                if (cur != null)
                    _progressText.text = $"Running: {cur.DisplayName}";
            }
        }

        // ── UI Construction ──────────────────────────────────────────────────────
        private void BuildUI()
        {
            bool vr = XRSetup.IsVRActive;

            // Canvas root
            var canvasGo = new GameObject("SessionUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();

            if (vr)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                var rt = canvasGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(800, 700);
                canvasGo.transform.localPosition = new Vector3(0f, 0f, 1.8f);
                canvasGo.transform.localScale = Vector3.one * 0.0018f;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;
            }

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Start Panel ──────────────────────────────────────────────────────
            _startPanel = MakeRect(canvasGo.transform, "StartPanel",
                new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.90f));
            var panelImg = _startPanel.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.1f, 0.96f);

            // Title
            var title = MakeText(_startPanel.transform, "Title",
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f),
                "DhrishtiLite", 34, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.85f, 0.95f));
            title.fontStyle = FontStyle.Bold;

            // Subtitle
            MakeText(_startPanel.transform, "Subtitle",
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.90f),
                "Ophthalmic Test Suite", 16, TextAnchor.MiddleCenter,
                new Color(0.45f, 0.55f, 0.6f));

            // ── Row 1: Patient ID ────────────────────────────────────────────────
            MakeText(_startPanel.transform, "PatLabel",
                new Vector2(0.06f, 0.72f), new Vector2(0.30f, 0.82f),
                "Patient ID", 18, TextAnchor.MiddleLeft, Color.white);

            var patGo = MakeRect(_startPanel.transform, "PatInput",
                new Vector2(0.32f, 0.72f), new Vector2(0.92f, 0.82f));
            patGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f);
            _patientField = patGo.AddComponent<InputField>();
            var patText = MakeText(patGo.transform, "Text",
                new Vector2(0.04f, 0f), Vector2.one, defaultPatientId, 18,
                TextAnchor.MiddleLeft, Color.white);
            _patientField.textComponent = patText;
            _patientField.text = defaultPatientId;

            // ── Row 2: Eye toggles ───────────────────────────────────────────────
            MakeText(_startPanel.transform, "EyeLabel",
                new Vector2(0.06f, 0.60f), new Vector2(0.20f, 0.70f),
                "Eye", 18, TextAnchor.MiddleLeft, Color.white);

            var toggleGroup = _startPanel.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;

            _odToggle = MakeToggle(_startPanel.transform, "OD",
                new Vector2(0.24f, 0.60f), new Vector2(0.44f, 0.70f),
                "OD", true, toggleGroup);
            _osToggle = MakeToggle(_startPanel.transform, "OS",
                new Vector2(0.48f, 0.60f), new Vector2(0.68f, 0.70f),
                "OS", false, toggleGroup);

            // ── Row 2b: Age ──────────────────────────────────────────────────────
            MakeText(_startPanel.transform, "AgeLabel",
                new Vector2(0.72f, 0.60f), new Vector2(0.82f, 0.70f),
                "Age", 18, TextAnchor.MiddleLeft, Color.white);

            var ageGo = MakeRect(_startPanel.transform, "AgeInput",
                new Vector2(0.84f, 0.60f), new Vector2(0.94f, 0.70f));
            ageGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f);
            _ageField = ageGo.AddComponent<InputField>();
            _ageField.contentType = InputField.ContentType.IntegerNumber;
            var ageText = MakeText(ageGo.transform, "Text",
                Vector2.zero, Vector2.one, defaultAge.ToString(), 18,
                TextAnchor.MiddleCenter, Color.white);
            _ageField.textComponent = ageText;
            _ageField.text = defaultAge.ToString();

            // ── Row 3: Test Selector ─────────────────────────────────────────────
            MakeText(_startPanel.transform, "TestLabel",
                new Vector2(0.06f, 0.48f), new Vector2(0.30f, 0.58f),
                "Test", 18, TextAnchor.MiddleLeft, Color.white);

            var ddGo = MakeRect(_startPanel.transform, "TestDropdown",
                new Vector2(0.32f, 0.48f), new Vector2(0.92f, 0.58f));
            ddGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f);
            _testDropdown = ddGo.AddComponent<Dropdown>();

            // Dropdown text
            var ddLabel = MakeText(ddGo.transform, "Label",
                new Vector2(0.06f, 0f), new Vector2(0.85f, 1f),
                "All Tests", 16, TextAnchor.MiddleLeft, Color.white);
            _testDropdown.captionText = ddLabel;

            // Dropdown arrow
            MakeText(ddGo.transform, "Arrow",
                new Vector2(0.88f, 0f), new Vector2(0.98f, 1f),
                "▼", 16, TextAnchor.MiddleCenter, new Color(0.5f, 0.7f, 0.8f));

            // Template (needed for Dropdown to work)
            var templateGo = MakeRect(ddGo.transform, "Template",
                new Vector2(0f, -3f), new Vector2(1f, 0f));
            templateGo.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f);
            var scrollRect = templateGo.AddComponent<ScrollRect>();

            var contentGo = MakeRect(templateGo.transform, "Content",
                Vector2.zero, Vector2.one);
            scrollRect.content = contentGo.GetComponent<RectTransform>();

            var itemGo = MakeRect(contentGo.transform, "Item",
                new Vector2(0f, 0.8f), new Vector2(1f, 1f));
            itemGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemLabel = MakeText(itemGo.transform, "Item Label",
                new Vector2(0.06f, 0f), Vector2.one,
                "", 16, TextAnchor.MiddleLeft, Color.white);

            _testDropdown.template = templateGo.GetComponent<RectTransform>();
            _testDropdown.itemText = itemLabel;
            templateGo.SetActive(false);

            // Populate dropdown
            PopulateTestDropdown();

            // ── Row 4: Language selector ────────────────────────────────────────
            MakeText(_startPanel.transform, "LangLabel",
                new Vector2(0.06f, 0.36f), new Vector2(0.30f, 0.46f),
                "Language", 18, TextAnchor.MiddleLeft, Color.white);

            var langGo = MakeRect(_startPanel.transform, "LangDropdown",
                new Vector2(0.32f, 0.36f), new Vector2(0.60f, 0.46f));
            langGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f);
            _langDropdown = langGo.AddComponent<Dropdown>();
            var langLabel = MakeText(langGo.transform, "Label",
                new Vector2(0.06f, 0f), new Vector2(0.85f, 1f),
                "English", 16, TextAnchor.MiddleLeft, Color.white);
            _langDropdown.captionText = langLabel;
            MakeText(langGo.transform, "Arrow",
                new Vector2(0.88f, 0f), new Vector2(0.98f, 1f),
                "▼", 14, TextAnchor.MiddleCenter, new Color(0.5f, 0.7f, 0.8f));
            // Lang dropdown template
            var langTmpl = MakeRect(langGo.transform, "Template",
                new Vector2(0f, -2f), new Vector2(1f, 0f));
            langTmpl.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f);
            var langScroll = langTmpl.AddComponent<ScrollRect>();
            var langContent = MakeRect(langTmpl.transform, "Content", Vector2.zero, Vector2.one);
            langScroll.content = langContent.GetComponent<RectTransform>();
            var langItem = MakeRect(langContent.transform, "Item",
                new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            langItem.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f);
            langItem.AddComponent<Toggle>();
            var langItemLabel = MakeText(langItem.transform, "Item Label",
                new Vector2(0.06f, 0f), Vector2.one, "", 16, TextAnchor.MiddleLeft, Color.white);
            _langDropdown.template = langTmpl.GetComponent<RectTransform>();
            _langDropdown.itemText = langItemLabel;
            langTmpl.SetActive(false);
            _langDropdown.ClearOptions();
            _langDropdown.AddOptions(new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("English"),
                new Dropdown.OptionData("ಕನ್ನಡ (Kannada)")
            });
            _langDropdown.value = 0;

            // ── Row 5: Quick Demo toggle ─────────────────────────────────────────
            _demoToggle = MakeToggle(_startPanel.transform, "DemoToggle",
                new Vector2(0.62f, 0.36f), new Vector2(0.94f, 0.46f),
                "Quick Demo", true, null);

            // ── Row 5: Start Button ──────────────────────────────────────────────
            var startGo = MakeRect(_startPanel.transform, "StartBtn",
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.28f));
            startGo.AddComponent<Image>().color = new Color(0.10f, 0.30f, 0.45f);
            _startBtn = startGo.AddComponent<Button>();
            var btnColors = _startBtn.colors;
            btnColors.highlightedColor = new Color(0.18f, 0.42f, 0.58f);
            btnColors.pressedColor = new Color(0.25f, 0.55f, 0.7f);
            _startBtn.colors = btnColors;
            MakeText(startGo.transform, "Label",
                Vector2.zero, Vector2.one,
                "▶  Start Session", 26, TextAnchor.MiddleCenter,
                new Color(0.7f, 0.95f, 1f)).fontStyle = FontStyle.Bold;

            _startBtn.onClick.AddListener(() => AudioFeedbackManager.Instance?.PlaySessionStart());
            _startBtn.onClick.AddListener(OnStartClicked);

            // VR hint
            if (vr)
            {
                MakeText(_startPanel.transform, "VRHint",
                    new Vector2(0.05f, 0.0f), new Vector2(0.95f, 0.07f),
                    "Point controller and squeeze trigger to respond",
                    16, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.55f));
            }

            // ── Stop Button (bottom-right, hidden initially) ─────────────────────
            var stopGo = MakeRect(canvasGo.transform, "StopBtn",
                new Vector2(0.78f, 0.02f), new Vector2(0.98f, 0.07f));
            stopGo.AddComponent<Image>().color = new Color(0.4f, 0.08f, 0.08f);
            _stopBtn = stopGo.AddComponent<Button>();
            MakeText(stopGo.transform, "Label",
                Vector2.zero, Vector2.one,
                "■ End Session", 16, TextAnchor.MiddleCenter, Color.white);
            _stopBtn.onClick.AddListener(() => AudioFeedbackManager.Instance?.PlayClick());
            _stopBtn.onClick.AddListener(OnStopClicked);
            _stopBtn.gameObject.SetActive(false);

            // ── Skip Button (bottom-center, hidden initially) ────────────────────
            var skipGo = MakeRect(canvasGo.transform, "SkipBtn",
                new Vector2(0.52f, 0.02f), new Vector2(0.76f, 0.07f));
            skipGo.AddComponent<Image>().color = new Color(0.35f, 0.30f, 0.08f);
            _skipBtn = skipGo.AddComponent<Button>();
            MakeText(skipGo.transform, "Label",
                Vector2.zero, Vector2.one,
                "⏭ Skip Test", 16, TextAnchor.MiddleCenter, Color.white);
            _skipBtn.onClick.AddListener(() => AudioFeedbackManager.Instance?.PlayClick());
            _skipBtn.onClick.AddListener(OnSkipClicked);
            _skipBtn.gameObject.SetActive(false);

            // ── Progress Text (bottom-left) ──────────────────────────────────────
            _progressText = MakeText(canvasGo.transform, "Progress",
                new Vector2(0.02f, 0.08f), new Vector2(0.50f, 0.12f),
                "", 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.8f, 1f));

            // ── Status Text ──────────────────────────────────────────────────────
            _statusText = MakeText(canvasGo.transform, "Status",
                new Vector2(0.02f, 0.0f), new Vector2(0.50f, 0.04f),
                "", 14, TextAnchor.MiddleLeft, new Color(0.3f, 1f, 0.5f));
        }

        // ── Test Dropdown ────────────────────────────────────────────────────────
        private void PopulateTestDropdown()
        {
            _testIds.Clear();
            var options = new List<Dropdown.OptionData>();

            // First option: run all tests
            options.Add(new Dropdown.OptionData("▶ All Tests (full battery)"));
            _testIds.Add("ALL");

            // Get registered tests from orchestrator
            if (_orchestrator != null)
            {
                // Access the test list by looking at what's wired
                var orchestratorTests = GetOrchestratorTests();
                foreach (var test in orchestratorTests)
                {
                    options.Add(new Dropdown.OptionData($"  {test.DisplayName}"));
                    _testIds.Add(test.TestId);
                }
            }

            // Fallback: add known test IDs if no modules found in scene
            if (_testIds.Count <= 1)
            {
                options.Add(new Dropdown.OptionData("  Humphrey Perimetry 24-2"));
                _testIds.Add("PERIMETRY_24_2");
                options.Add(new Dropdown.OptionData("  CSV-1000 contrast sensitivity"));
                _testIds.Add("CSV_1000");
                options.Add(new Dropdown.OptionData("  Pelli-Robson contrast sensitivity"));
                _testIds.Add("PELLI_ROBSON");
                options.Add(new Dropdown.OptionData("  SPARCS (4-quadrant spatial)"));
                _testIds.Add("SPARCS");
                options.Add(new Dropdown.OptionData("  Motion detection (RDK)"));
                _testIds.Add("MOTION_DETECTION");
                options.Add(new Dropdown.OptionData("  Edge detection"));
                _testIds.Add("EDGE_DETECTION");
                options.Add(new Dropdown.OptionData("  Pattern detection"));
                _testIds.Add("PATTERN_DETECTION");
            }

            _testDropdown.ClearOptions();
            _testDropdown.AddOptions(options);
            _testDropdown.value = 0;
        }

        private List<ITestModule> GetOrchestratorTests()
        {
            var result = new List<ITestModule>();

            // Try VisualTestManager first (ordered list)
            var vtm = FindFirstObjectByType<VisualTestManager>();
            if (vtm != null)
            {
                result.AddRange(vtm.GetModulesInOrder());
                if (result.Count > 0) return result;
            }

            // Fallback: find all ITestModule MonoBehaviours in scene
            var allMono = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMono)
            {
                if (mb is ITestModule mod && !result.Contains(mod))
                    result.Add(mod);
            }
            return result;
        }

        // ── Callbacks ────────────────────────────────────────────────────────────
        private void OnStartClicked()
        {
            if (_orchestrator == null)
            {
                ShowStatus("Error: AppOrchestrator not found!", 4f);
                return;
            }

            string pid = _patientField.text.Trim();
            if (string.IsNullOrEmpty(pid)) pid = defaultPatientId;

            string eye = _odToggle.isOn ? "OD" : "OS";
            int.TryParse(_ageField.text, out int age);
            if (age <= 0) age = defaultAge;

            // Configure eye occlusion in VR
            var occluder = FindFirstObjectByType<EyeOccluder>();
            if (occluder != null) occluder.SetTestedEye(eye);

            // Quick Demo mode — reduce perimetry trials
            if (_demoToggle != null && _demoToggle.isOn)
            {
                var master = FindFirstObjectByType<global::Perimetry.PerimetryMaster>();
                if (master != null)
                {
                    SetPrivateField(master, "questMaxTrialsPerLocus", 2);
                    SetPrivateField(master, "sitaLikePresentationOrder", true);
                    Debug.Log("SessionStartUI: Quick Demo — reduced to ~2 trials/locus.");
                }
            }

            // Language mode for Pelli-Robson
            if (_langDropdown != null)
            {
                var pelliMod = FindFirstObjectByType<global::OphthalSuite.ContrastSensitivity.PelliRobson.PelliRobsonModule>();
                if (pelliMod != null)
                {
                    // 0 = EnglishOnly, 1 = KannadaOnly
                    int langVal = _langDropdown.value; // 0 English, 1 Kannada
                    SetPrivateField(pelliMod, "scriptMode", (global::OphthalSuite.ContrastSensitivity.PelliRobson.PelliScriptMode)langVal);
                    Debug.Log($"SessionStartUI: Pelli-Robson language set to {(_langDropdown.value == 0 ? "English" : "Kannada")}.");
                }
            }

            // Determine which tests to run
            int ddIdx = _testDropdown != null ? _testDropdown.value : 0;
            string selectedId = ddIdx >= 0 && ddIdx < _testIds.Count ? _testIds[ddIdx] : "ALL";

            if (selectedId == "ALL")
            {
                _orchestrator.StartSession(pid, eye, age);
            }
            else
            {
                _orchestrator.StartSession(pid, eye, age, new List<string> { selectedId });
            }

            _startPanel.SetActive(false);
            _stopBtn.gameObject.SetActive(true);
            _skipBtn.gameObject.SetActive(true);
            ShowStatus($"Session: {pid} / {eye} → {(selectedId == "ALL" ? "All Tests" : selectedId)}", 3f);
        }

        private void OnStopClicked()
        {
            if (_orchestrator != null)
            {
                _orchestrator.EndSession();
                var occluder = FindFirstObjectByType<EyeOccluder>();
                if (occluder != null) occluder.ClearOcclusion();
            }

            _stopBtn.gameObject.SetActive(false);
            _skipBtn.gameObject.SetActive(false);
            if (_progressText != null) _progressText.text = "";
            _startPanel.SetActive(true);
            ShowStatus("Session ended.", 3f);
        }

        private void OnSkipClicked()
        {
            if (_orchestrator == null) return;

            // Stop the current running test — AppOrchestrator will move to next
            var curTest = _orchestrator.CurrentTest;
            if (curTest != null && curTest.IsRunning)
            {
                curTest.StopTest();
                ShowStatus($"Skipped: {curTest.DisplayName}", 2f);
            }
        }

        private void ShowStatus(string msg, float dur)
        {
            if (_statusText != null) _statusText.text = msg;
            _statusTimer = dur;
        }

        // ── Reflection helper to set private serialized fields ───────────────────
        private static void SetPrivateField(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(obj, value);
        }

        // ── UI Helpers ───────────────────────────────────────────────────────────
        private static GameObject MakeRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, TextAnchor align, Color color)
        {
            var go = MakeRect(parent, name, anchorMin, anchorMax);
            var t = go.AddComponent<Text>();
            t.font = uiFont != null ? uiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private Toggle MakeToggle(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string label, bool isOn, ToggleGroup group)
        {
            var go = MakeRect(parent, name, anchorMin, anchorMax);
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.2f);

            var toggle = go.AddComponent<Toggle>();
            if (group != null) toggle.group = group;
            toggle.isOn = isOn;

            // Checkmark
            var checkGo = MakeRect(go.transform, "Checkmark",
                new Vector2(0.05f, 0.15f), new Vector2(0.2f, 0.85f));
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.8f, 0.9f);
            toggle.graphic = checkImg;

            // Label
            MakeText(go.transform, "Label",
                new Vector2(0.25f, 0f), new Vector2(1f, 1f),
                label, 18, TextAnchor.MiddleLeft, Color.white);

            return toggle;
        }
    }
}
