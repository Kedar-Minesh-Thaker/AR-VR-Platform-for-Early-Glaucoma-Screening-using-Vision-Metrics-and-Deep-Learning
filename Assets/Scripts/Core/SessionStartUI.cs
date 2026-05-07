// SessionStartUI.cs
// Premium "Midnight Violet" UI for DhrishtiLite.
// Features glassmorphism, smooth animations, and VR-optimized test selection cards.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OphthalSuite.Core;
using OphthalSuite.Core.Database;

namespace OphthalSuite.Core
{
    public class SessionStartUI : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private Font uiFont;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.5f;

        // ── Internal State ───────────────────────────────────────────────────────
        private AppOrchestrator _orchestrator;
        private CanvasGroup _canvasGroup;
        private GameObject _mainPanel;
        private GameObject _dataPanel;
        // Emergency collider removed — VRPointer now handles controller interaction properly

        private Text _dataText;
        private bool _isFadingOut;
        
        // Input Fields
        private InputField _patientField;
        private InputField _ageField;
        private Toggle _odToggle;
        private Toggle _osToggle;
        private Toggle _demoToggle;
        
        // Test Cards
        private string _selectedTestId = "ALL";
        private List<TestCard> _testCards = new List<TestCard>();
        private int _patientCounter = 1;  // auto-incremented per session

        // ── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _orchestrator = GetComponent<AppOrchestrator>() ?? FindFirstObjectByType<AppOrchestrator>();
        }

        // EnsureEditorMouseInput removed — VRPointer.SetupEventSystem() handles input modules
        // without the race condition that was adding StandaloneInputModule before XR init.

        private IEnumerator Start()
        {
            yield return WaitForXRDecision();
            BuildPremiumUI();
            StartCoroutine(FadeIn());
            SetFixationDotVisible(false);
        }



        private static IEnumerator WaitForXRDecision()
        {
            const float maxWaitSeconds = 2f;
            float start = Time.realtimeSinceStartup;
            while (!XRSetup.IsInitialised && Time.realtimeSinceStartup - start < maxWaitSeconds)
                yield return null;
        }

        private void Update()
        {
            if (_mainPanel == null) return;

            // Auto-hide if session starts externally
            if (_orchestrator != null && _orchestrator.IsSessionRunning && _mainPanel.activeSelf && !_isFadingOut)
            {
                StartCoroutine(FadeOutAndDisable());
            }
            
            // Auto-show if session ends — also auto-increment patient ID
            if (_orchestrator != null && !_orchestrator.IsSessionRunning && !_mainPanel.activeSelf)
            {
                _mainPanel.SetActive(true);
                AutoIncrementPatientId();
                SetFixationDotVisible(false);
                StartCoroutine(FadeIn());
            }
        }

        // ── UI Construction ──────────────────────────────────────────────────────

        private void BuildPremiumUI()
        {
            // 1. Root Canvas setup
            var canvasGo = new GameObject("SessionUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            
            VRCanvasUtil.ConfigureFloatingMonitorCanvas(canvas, canvasGo, 100);
            canvasGo.AddComponent<GraphicRaycaster>();
            VRPointer.UpgradeCanvasForVR(canvasGo);

            // 2. Background Overlay (Subtle Gradient)
            var bgImg = UIStyleKit.MakeImage(canvasGo.transform, "Background", Vector2.zero, Vector2.one, null, UIStyleKit.Colors.BgDark);
            bgImg.color = new Color(UIStyleKit.Colors.BgDark.r, UIStyleKit.Colors.BgDark.g, UIStyleKit.Colors.BgDark.b, 0.6f);

            // 3. Main Glassmorphism Card
            _mainPanel = UIStyleKit.MakeRect(canvasGo.transform, "MainPanel", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            var cardImg = _mainPanel.AddComponent<Image>();
            cardImg.sprite = UIStyleKit.MakeRoundedRect(1000, 750, 40, UIStyleKit.Colors.BgCard, UIStyleKit.Colors.BgCardBorder, 2);
            cardImg.type = Image.Type.Sliced;

            // Header Section
            UIStyleKit.MakeText(_mainPanel.transform, "Title", new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), "DhrishtiLite", UIStyleKit.FontSize.Title, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextTitle, uiFont).fontStyle = FontStyle.Bold;
            UIStyleKit.MakeText(_mainPanel.transform, "Subtitle", new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.90f), "Visual Field Assessment Suite", UIStyleKit.FontSize.Subtitle, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextSecondary, uiFont);

            // Patient Inputs Row
            CreateInputSection(_mainPanel.transform);

            // Test Selection Grid
            CreateTestGrid(_mainPanel.transform);

            // Action Buttons
            CreateActionButtons(_mainPanel.transform);

            // Historical data viewer
            CreateDataPanel(_mainPanel.transform);
        }

        private void CreateInputSection(Transform parent)
        {
            float rowY = 0.72f;
            
            // Patient ID
            UIStyleKit.MakeText(parent, "IDLabel", new Vector2(0.08f, rowY), new Vector2(0.3f, rowY + 0.08f), "PATIENT ID", UIStyleKit.FontSize.Label, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextSecondary, uiFont);
            var patGo = UIStyleKit.MakeRect(parent, "PatInput", new Vector2(0.24f, rowY), new Vector2(0.55f, rowY + 0.08f));
            patGo.AddComponent<Image>().sprite = UIStyleKit.MakeRoundedRect(400, 80, 15, UIStyleKit.Colors.BgInput);
            _patientField = patGo.AddComponent<InputField>();
            var patText = UIStyleKit.MakeText(patGo.transform, "Text", new Vector2(0.05f, 0f), Vector2.one, "P001", UIStyleKit.FontSize.Body, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextPrimary, uiFont);
            _patientField.textComponent = patText;
            _patientField.text = "P001";

            // Age
            UIStyleKit.MakeText(parent, "AgeLabel", new Vector2(0.60f, rowY), new Vector2(0.75f, rowY + 0.08f), "AGE", UIStyleKit.FontSize.Label, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextSecondary, uiFont);
            var ageGo = UIStyleKit.MakeRect(parent, "AgeInput", new Vector2(0.72f, rowY), new Vector2(0.92f, rowY + 0.08f));
            ageGo.AddComponent<Image>().sprite = UIStyleKit.MakeRoundedRect(200, 80, 15, UIStyleKit.Colors.BgInput);
            _ageField = ageGo.AddComponent<InputField>();
            _ageField.contentType = InputField.ContentType.IntegerNumber;
            var ageText = UIStyleKit.MakeText(ageGo.transform, "Text", Vector2.zero, Vector2.one, "55", UIStyleKit.FontSize.Body, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary, uiFont);
            _ageField.textComponent = ageText;
            _ageField.text = "55";

            // Eye Toggles
            float eyeY = 0.62f;
            UIStyleKit.MakeText(parent, "EyeLabel", new Vector2(0.08f, eyeY), new Vector2(0.25f, eyeY + 0.06f), "SELECT EYE", UIStyleKit.FontSize.Label, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextSecondary, uiFont);
            
            var groupGo = new GameObject("EyeGroup", typeof(ToggleGroup));
            groupGo.transform.SetParent(parent, false);
            var group = groupGo.GetComponent<ToggleGroup>();
            group.allowSwitchOff = false;

            _odToggle = CreatePillToggle(parent, "RIGHT (OD)", new Vector2(0.24f, eyeY), new Vector2(0.44f, eyeY + 0.06f), true, group);
            _osToggle = CreatePillToggle(parent, "LEFT (OS)", new Vector2(0.46f, eyeY), new Vector2(0.66f, eyeY + 0.06f), false, group);
            
            _demoToggle = CreatePillToggle(parent, "QUICK DEMO", new Vector2(0.72f, eyeY), new Vector2(0.92f, eyeY + 0.06f), true, null);
        }

        private void CreateTestGrid(Transform parent)
        {
            UIStyleKit.MakeText(parent, "GridLabel", new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.58f), "SELECT CLINICAL TEST", UIStyleKit.FontSize.Label, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextSecondary, uiFont);

            var gridGo = UIStyleKit.MakeRect(parent, "TestGrid", new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.52f));
            
            // All 7 tests + Full Battery = 8 cards in a 2×4 grid
            string[] testNames = { "Full Battery", "Perimetry 24-2", "Pelli-Robson Rows", "CSV Letters",
                                   "Edge Tracing", "Pattern Tracing", "Motion Detection", "SPARCS Quadrants" };
            string[] testIds   = { "ALL", "PERIMETRY_24_2", "PELLI_ROBSON", "CSV_1000",
                                   "EDGE_DETECTION", "PATTERN_DETECTION", "MOTION_DETECTION", "SPARCS" };

            int cols = 4;
            for (int i = 0; i < testNames.Length; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float xMin = col * 0.25f + 0.005f;
                float xMax = (col + 1) * 0.25f - 0.005f;
                float yMax = 1f - row * 0.52f;
                float yMin = yMax - 0.48f;

                var card = CreateTestCard(gridGo.transform, testNames[i], testIds[i],
                    new Vector2(xMin, yMin), new Vector2(xMax, yMax));
                _testCards.Add(card);
            }
            
            UpdateCardSelection();
        }

        private void CreateActionButtons(Transform parent)
        {
            var startGo = UIStyleKit.MakeRect(parent, "StartBtn", new Vector2(0.10f, 0.05f), new Vector2(0.64f, 0.20f));
            var btnImg = startGo.AddComponent<Image>();
            btnImg.sprite = UIStyleKit.MakeRoundedRect(700, 120, 30, UIStyleKit.Colors.BtnPrimary);
            btnImg.type = Image.Type.Sliced;
            
            var btn = startGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UIStyleKit.Colors.BtnPrimaryHover;
            colors.pressedColor = UIStyleKit.Colors.BtnPrimaryPress;
            btn.colors = colors;

            UIStyleKit.MakeText(startGo.transform, "Label", Vector2.zero, Vector2.one, "BEGIN CLINICAL SESSION", UIStyleKit.FontSize.Button, TextAnchor.MiddleCenter, Color.white, uiFont).fontStyle = FontStyle.Bold;

            btn.onClick.AddListener(OnStartClicked);
            
            // Add a subtle pulse to the start button
            StartCoroutine(PulseButton(btnImg));

            var dataGo = UIStyleKit.MakeRect(parent, "ViewDataBtn", new Vector2(0.67f, 0.05f), new Vector2(0.90f, 0.20f));
            var dataImg = dataGo.AddComponent<Image>();
            dataImg.sprite = UIStyleKit.MakeRoundedRect(280, 120, 28,
                new Color(0.10f, 0.08f, 0.18f, 0.95f),
                UIStyleKit.Colors.BgCardBorder, 1);
            dataImg.type = Image.Type.Sliced;
            var dataBtn = dataGo.AddComponent<Button>();
            UIStyleKit.MakeText(dataGo.transform, "Label", Vector2.zero, Vector2.one,
                "VIEW\nDATA", UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter,
                UIStyleKit.Colors.TextPrimary, uiFont).fontStyle = FontStyle.Bold;
            dataBtn.onClick.AddListener(ShowDataPanel);
        }

        private void CreateDataPanel(Transform parent)
        {
            _dataPanel = UIStyleKit.MakeRect(parent, "DataPanel", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            _dataPanel.SetActive(false);
            var bg = _dataPanel.AddComponent<Image>();
            bg.sprite = UIStyleKit.MakeRoundedRect(1000, 900, 35,
                new Color(0.04f, 0.035f, 0.08f, 0.98f),
                new Color(0.55f, 0.35f, 0.82f, 0.45f), 2);
            bg.type = Image.Type.Sliced;

            UIStyleKit.MakeText(_dataPanel.transform, "Title", new Vector2(0.04f, 0.90f), new Vector2(0.80f, 0.98f),
                "Stored Patient Data", UIStyleKit.FontSize.Heading, TextAnchor.MiddleLeft,
                UIStyleKit.Colors.TextTitle, uiFont).fontStyle = FontStyle.Bold;

            CreateDataUtilityButton("Refresh", new Vector2(0.68f, 0.91f), new Vector2(0.82f, 0.97f), RefreshDataPanel);
            CreateDataUtilityButton("Close", new Vector2(0.84f, 0.91f), new Vector2(0.96f, 0.97f), HideDataPanel);

            _dataText = UIStyleKit.MakeText(_dataPanel.transform, "DataText", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.88f),
                "", UIStyleKit.FontSize.Small, TextAnchor.UpperLeft, UIStyleKit.Colors.TextPrimary, uiFont);
            _dataText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _dataText.verticalOverflow = VerticalWrapMode.Overflow;
            _dataText.lineSpacing = 1.15f;
        }

        private void CreateDataUtilityButton(string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = UIStyleKit.MakeRect(_dataPanel.transform, label, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(220, 80, 18,
                new Color(0.10f, 0.08f, 0.16f, 0.95f),
                UIStyleKit.Colors.BgCardBorder, 1);
            img.type = Image.Type.Sliced;
            var btn = go.AddComponent<Button>();
            UIStyleKit.MakeText(go.transform, "Label", Vector2.zero, Vector2.one,
                label, UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter,
                UIStyleKit.Colors.TextPrimary, uiFont);
            btn.onClick.AddListener(action);
        }

        // ── UI Factory Helpers ───────────────────────────────────────────────────

        private Toggle CreatePillToggle(Transform parent, string label, Vector2 min, Vector2 max, bool isOn, ToggleGroup group)
        {
            var go = UIStyleKit.MakeRect(parent, label, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(200, 60, 30, UIStyleKit.Colors.ToggleInactive);
            img.type = Image.Type.Sliced;

            var toggle = go.AddComponent<Toggle>();
            toggle.group = group;
            toggle.isOn = isOn;

            var labelText = UIStyleKit.MakeText(go.transform, "Label", Vector2.zero, Vector2.one, label, UIStyleKit.FontSize.ButtonSm, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextSecondary, uiFont);
            
            toggle.onValueChanged.AddListener((val) => {
                img.color = val ? UIStyleKit.Colors.ToggleActive : Color.white;
                labelText.color = val ? Color.white : UIStyleKit.Colors.TextSecondary;
                labelText.fontStyle = val ? FontStyle.Bold : FontStyle.Normal;
                if (val) AudioFeedbackManager.Instance?.PlayClick();
            });
            
            // Initial state
            img.color = isOn ? UIStyleKit.Colors.ToggleActive : Color.white;
            labelText.color = isOn ? Color.white : UIStyleKit.Colors.TextSecondary;
            
            return toggle;
        }

        private TestCard CreateTestCard(Transform parent, string label, string id, Vector2 min, Vector2 max)
        {
            var go = UIStyleKit.MakeRect(parent, id, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(200, 200, 25, UIStyleKit.Colors.BgInput, UIStyleKit.Colors.BgCardBorder, 1);
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            UIStyleKit.MakeText(go.transform, "Label", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.46f), label, UIStyleKit.FontSize.ButtonSm, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary, uiFont);
            
            // Icon Placeholder
            var iconGo = UIStyleKit.MakeRect(go.transform, "Icon", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.9f));
            iconGo.AddComponent<Image>().sprite = UIStyleKit.MakeCircleSprite(64, UIStyleKit.Colors.AccentGlow);

            btn.onClick.AddListener(() => {
                _selectedTestId = id;
                UpdateCardSelection();
                AudioFeedbackManager.Instance?.PlayClick();
            });

            return new TestCard { id = id, image = img };
        }

        private void UpdateCardSelection()
        {
            foreach (var card in _testCards)
            {
                bool sel = card.id == _selectedTestId;
                card.image.color = sel ? UIStyleKit.Colors.Accent : Color.white;
                card.image.pixelsPerUnitMultiplier = sel ? 1.2f : 1f;
            }
        }

        // ── Callbacks ────────────────────────────────────────────────────────────

        private void OnStartClicked()
        {
            if (_orchestrator == null)
                _orchestrator = GetComponent<AppOrchestrator>() ?? FindFirstObjectByType<AppOrchestrator>();
            if (_orchestrator == null)
            {
                Debug.LogError("SessionStartUI: Cannot begin session because AppOrchestrator was not found.");
                return;
            }
            
            string pid = _patientField.text.Trim();
            string eye = _odToggle.isOn ? "OD" : "OS";
            int.TryParse(_ageField.text, out int age);
            if (age <= 0)
            {
                age = 55;
                _ageField.text = "55";
            }
            
            AudioFeedbackManager.Instance?.PlaySessionStart();
            SetFixationDotVisible(true);



            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (_selectedTestId == "ALL")
                _orchestrator.StartSession(pid, eye, age);
            else
                _orchestrator.StartSession(pid, eye, age, BuildBatteryStartingWith(_selectedTestId));

            if (_orchestrator.IsSessionRunning)
            {
                StartCoroutine(FadeOutAndDisable());
            }
            else
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
        }

        private void ShowDataPanel()
        {
            if (_dataPanel == null) return;
            RefreshDataPanel();
            _dataPanel.SetActive(true);
            AudioFeedbackManager.Instance?.PlayClick();
        }

        private void HideDataPanel()
        {
            if (_dataPanel == null) return;
            _dataPanel.SetActive(false);
            AudioFeedbackManager.Instance?.PlayClick();
        }

        private void RefreshDataPanel()
        {
            if (_dataText == null) return;

            var db = DatabaseManager.Instance;
            if (db == null)
            {
                _dataText.text = "DatabaseManager not found.\nRun a session once or ensure the DatabaseManager object exists in the scene.";
                return;
            }

            var sessions = db.GetAllSessions();
            var sb = new StringBuilder();
            sb.AppendLine("Local device database");
            sb.AppendLine(Application.persistentDataPath + "/db");
            sb.AppendLine();

            if (sessions.Count == 0)
            {
                sb.AppendLine("No previous patient sessions stored yet.");
                _dataText.text = sb.ToString();
                return;
            }

            int shown = 0;
            foreach (var s in sessions)
            {
                if (shown++ >= 12) break;
                var trials = db.GetTrialsBySession(s.id);
                var results = db.GetResultsBySession(s.id);
                int hits = 0;
                foreach (var t in trials)
                    if (t.hit) hits++;

                float accuracy = trials.Count > 0 ? hits / (float)trials.Count : 0f;
                sb.AppendLine($"Patient {s.patientId} | Eye {s.eye} | {s.status}");
                sb.AppendLine($"Session: {s.id}");
                sb.AppendLine($"Started: {s.startedAt}    Ended: {s.endedAt}");
                sb.AppendLine($"Duration: {s.durationSec:F1}s    Trials: {trials.Count}    Tests: {results.Count}    Accuracy: {accuracy:P0}");

                foreach (var r in results)
                {
                    sb.AppendLine($"  - {r.testId}: {r.reliability}, duration {r.durationSec:F1}s, FP {r.falsePosRate:P0}, FN {r.falseNegRate:P0}, fixation {r.fixationLoss:P0}");
                }

                sb.AppendLine("  ML fields captured per trial: testId, stimulusId, hit/miss, RT, fixationLost, timestamp, extraJson");
                sb.AppendLine();
            }

            _dataText.text = sb.ToString();
        }

        /// <summary>Auto-increment patient ID when returning from a completed session.</summary>
        private void AutoIncrementPatientId()
        {
            _patientCounter++;
            string newId = $"P{_patientCounter:D3}";
            if (_patientField != null)
                _patientField.text = newId;
        }

        private static List<string> BuildBatteryStartingWith(string firstTestId)
        {
            var canonical = new List<string>
            {
                "PERIMETRY_24_2",
                "PELLI_ROBSON",
                "CSV_1000",
                "EDGE_DETECTION",
                "PATTERN_DETECTION",
                "MOTION_DETECTION",
                "SPARCS"
            };

            var ordered = new List<string>();
            if (!string.IsNullOrEmpty(firstTestId) && firstTestId != "ALL")
                ordered.Add(firstTestId);

            foreach (var id in canonical)
            {
                if (!ordered.Contains(id))
                    ordered.Add(id);
            }

            return ordered;
        }

        // ── Animations ───────────────────────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            _isFadingOut = false;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            // Re-enable the child canvas if it was disabled
            var childCanvas = GetComponentInChildren<Canvas>(true);
            if (childCanvas != null) childCanvas.enabled = true;

            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = UIStyleKit.EaseOut(t / fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1;
            SetFixationDotVisible(false);
        }

        private IEnumerator FadeOutAndDisable()
        {
            if (_isFadingOut) yield break;
            _isFadingOut = true;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = 1f - UIStyleKit.EaseIn(t / fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _mainPanel.SetActive(false);

            // Disable the child canvas entirely so it can't intercept anything
            var childCanvas = GetComponentInChildren<Canvas>(true);
            if (childCanvas != null) childCanvas.enabled = false;
            _isFadingOut = false;
        }

        private static void SetFixationDotVisible(bool visible)
        {
            var cam = Camera.main;
            var dot = cam != null ? cam.transform.Find("FixationDot") : null;
            if (dot != null)
                dot.gameObject.SetActive(visible);
        }

        private IEnumerator PulseButton(Image img)
        {
            while (true)
            {
                float p = UIStyleKit.PulseSine(Time.time, 2f);
                img.color = Color.Lerp(UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.BtnPrimaryHover, p);
                img.transform.localScale = Vector3.one * (1f + p * 0.03f);
                yield return null;
            }
        }

        private class TestCard { public string id; public Image image; }
    }
}
