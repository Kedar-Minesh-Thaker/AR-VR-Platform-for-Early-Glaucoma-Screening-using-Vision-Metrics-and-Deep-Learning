// PatientHUD.cs
// In-test overlay for DhrishtiLite with operator controls.
// Shows test progress, per-test instructions, and action buttons:
//   • SKIP TEST — saves partial data, advances to next test
//   • MAIN MENU — saves partial data, returns to session start screen
//   • Session progress counter (e.g., "3 of 7")

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace OphthalSuite.Core
{
    public class PatientHUD : MonoBehaviour
    {
        private AppOrchestrator _orchestrator;
        private CanvasGroup _canvasGroup;
        private Image _progressFill;
        private Text _instructionText;
        private Text _counterText;
        private Text _sessionCounterText;
        private GameObject _controlPanel;
        private GameObject _testSwitchPanel;
        private bool _built;

        [SerializeField] private float fadeDuration = 0.8f;

        private void Awake()
        {
            _orchestrator = FindFirstObjectByType<AppOrchestrator>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            
            StartCoroutine(BuildWhenXRReady());
        }

        private IEnumerator BuildWhenXRReady()
        {
            const float maxWaitSeconds = 2f;
            float start = Time.realtimeSinceStartup;
            while (!XRSetup.IsInitialised && Time.realtimeSinceStartup - start < maxWaitSeconds)
                yield return null;

            BuildHUD();
            _built = true;
        }

        private bool _isFading;
        private bool _isVisible;

        private void Update()
        {
            if (!_built) return;
            if (_orchestrator == null) return;

            bool shouldShow = _orchestrator.IsSessionRunning && _orchestrator.CurrentTest != null;
            
            // Fade in when test starts
            if (shouldShow && !_isVisible && !_isFading)
            {
                _isFading = true;
                StopAllCoroutines();
                StartCoroutine(Fade(1));
            }
            // Fade out when test ends
            else if (!shouldShow && _isVisible && !_isFading)
            {
                _isFading = true;
                StopAllCoroutines();
                StartCoroutine(Fade(0));
            }

            // Update Progress
            if (shouldShow)
            {
                UpdateProgress();
                UpdateSessionCounter();
            }
        }

        private void BuildHUD()
        {
            // Root Canvas for HUD
            var canvasGo = new GameObject("PatientHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            VRCanvasUtil.ConfigureFloatingMonitorCanvas(canvas, canvasGo, 1000);
            canvasGo.AddComponent<GraphicRaycaster>();
            VRPointer.UpgradeCanvasForVR(canvasGo);

            // 1. Progress Ring (Top Left)
            var ringRoot = UIStyleKit.MakeRect(canvasGo.transform, "ProgressRing", new Vector2(0.05f, 0.85f), new Vector2(0.15f, 0.95f));
            var track = ringRoot.AddComponent<Image>();
            track.sprite = UIStyleKit.MakeRingSprite(128, 8, UIStyleKit.Colors.ProgressTrack);
            
            var fillGo = UIStyleKit.MakeRect(ringRoot.transform, "Fill", Vector2.zero, Vector2.one);
            _progressFill = fillGo.AddComponent<Image>();
            _progressFill.sprite = UIStyleKit.MakeRingSprite(128, 8, Color.white);
            _progressFill.color = UIStyleKit.Colors.ProgressFill;
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Radial360;
            _progressFill.fillOrigin = (int)Image.Origin360.Top;

            _counterText = UIStyleKit.MakeText(ringRoot.transform, "Counter", new Vector2(0f, -0.5f), new Vector2(1f, 0f), "0%", UIStyleKit.FontSize.HudSmall, TextAnchor.UpperCenter, UIStyleKit.Colors.TextSecondary);

            // 2. Session counter (Top Right) — "3 of 7 tests"
            _sessionCounterText = UIStyleKit.MakeText(canvasGo.transform, "SessionCounter",
                new Vector2(0.65f, 0.90f), new Vector2(0.95f, 0.96f),
                "1 of 7", UIStyleKit.FontSize.HudSmall, TextAnchor.MiddleRight, UIStyleKit.Colors.TextSecondary);

            // 3. In-test modules already show their own prompts; keep this hidden
            // so stale instruction text never lingers over the active test.
            _instructionText = UIStyleKit.MakeText(canvasGo.transform, "Instructions", new Vector2(0.1f, 0.20f), new Vector2(0.9f, 0.32f), "", UIStyleKit.FontSize.HudLarge, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary);
            _instructionText.gameObject.SetActive(false);
            
            // Add a soft glow to the text
            var shadow = _instructionText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(UIStyleKit.Colors.AccentGlow.r, UIStyleKit.Colors.AccentGlow.g, UIStyleKit.Colors.AccentGlow.b, 0.5f);
            shadow.effectDistance = new Vector2(0, 0);

            // ── 4. Operator Control Panel (Bottom, shared for every test) ─────────
            BuildControlPanel(canvasGo.transform);
            BuildTestSwitchPanel(canvasGo.transform);
        }

        private void BuildControlPanel(Transform parent)
        {
            // Small bottom-corner controls so they do not cover test response buttons.
            _controlPanel = UIStyleKit.MakeRect(parent, "ControlPanel",
                new Vector2(0.00f, 0.00f), new Vector2(1.00f, 0.08f));

            // Semi-transparent background
            var panelBg = _controlPanel.AddComponent<Image>();
            panelBg.sprite = UIStyleKit.MakeRoundedRect(300, 80, 18,
                new Color(0.06f, 0.04f, 0.12f, 0.0f),
                new Color(0.5f, 0.3f, 0.8f, 0.0f), 0);
            panelBg.type = Image.Type.Sliced;
            panelBg.raycastTarget = false;

            // SKIP TEST button (left half)
            var skipGo = UIStyleKit.MakeRect(_controlPanel.transform, "SkipBtn",
                new Vector2(0.04f, 0.10f), new Vector2(0.18f, 0.90f));
            var skipImg = skipGo.AddComponent<Image>();
            skipImg.sprite = UIStyleKit.MakeRoundedRect(150, 60, 14,
                new Color(0.15f, 0.10f, 0.08f, 0.9f),
                UIStyleKit.Colors.Warning, 1);
            skipImg.type = Image.Type.Sliced;

            var skipBtn = skipGo.AddComponent<Button>();
            var skipColors = skipBtn.colors;
            skipColors.highlightedColor = UIStyleKit.Colors.BtnWarning;
            skipColors.pressedColor = UIStyleKit.Colors.Warning;
            skipBtn.colors = skipColors;

            UIStyleKit.MakeText(skipGo.transform, "Label",
                Vector2.zero, Vector2.one, "SKIP",
                UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.Warning);

            skipBtn.onClick.AddListener(OnSkipTest);

            // MENU button (right half)
            var menuGo = UIStyleKit.MakeRect(_controlPanel.transform, "MenuBtn",
                new Vector2(0.82f, 0.10f), new Vector2(0.96f, 0.90f));
            var menuImg = menuGo.AddComponent<Image>();
            menuImg.sprite = UIStyleKit.MakeRoundedRect(150, 60, 14,
                new Color(0.12f, 0.05f, 0.05f, 0.9f),
                UIStyleKit.Colors.Danger, 1);
            menuImg.type = Image.Type.Sliced;

            var menuBtn = menuGo.AddComponent<Button>();
            var menuColors = menuBtn.colors;
            menuColors.highlightedColor = UIStyleKit.Colors.BtnDangerHover;
            menuColors.pressedColor = UIStyleKit.Colors.Danger;
            menuBtn.colors = menuColors;

            UIStyleKit.MakeText(menuGo.transform, "Label",
                Vector2.zero, Vector2.one, "MENU",
                UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.Danger);

            menuBtn.onClick.AddListener(OnMainMenu);
        }

        private void BuildTestSwitchPanel(Transform parent)
        {
            _testSwitchPanel = UIStyleKit.MakeRect(parent, "TestSwitchPanel",
                new Vector2(0.18f, 0.16f), new Vector2(0.82f, 0.78f));
            _testSwitchPanel.SetActive(false);

            var bg = _testSwitchPanel.AddComponent<Image>();
            bg.sprite = UIStyleKit.MakeRoundedRect(700, 900, 32,
                new Color(0.05f, 0.04f, 0.10f, 0.96f),
                new Color(0.55f, 0.35f, 0.80f, 0.45f), 2);
            bg.type = Image.Type.Sliced;

            UIStyleKit.MakeText(_testSwitchPanel.transform, "Title",
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f),
                "Switch Test", UIStyleKit.FontSize.Heading, TextAnchor.MiddleCenter,
                UIStyleKit.Colors.TextTitle).fontStyle = FontStyle.Bold;

            var tests = new (string label, string id)[]
            {
                ("Perimetry 24-2", "PERIMETRY_24_2"),
                ("CSV Letters", "CSV_1000"),
                ("Pelli-Robson Rows", "PELLI_ROBSON"),
                ("SPARCS Quadrants", "SPARCS"),
                ("Motion Detection", "MOTION_DETECTION"),
                ("Edge Tracing", "EDGE_DETECTION"),
                ("Pattern Tracing", "PATTERN_DETECTION")
            };

            for (int i = 0; i < tests.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                float x0 = col == 0 ? 0.07f : 0.52f;
                float x1 = col == 0 ? 0.48f : 0.93f;
                float y1 = 0.82f - row * 0.14f;
                float y0 = y1 - 0.10f;
                CreateSwitchButton(tests[i].label, tests[i].id, new Vector2(x0, y0), new Vector2(x1, y1));
            }

            CreateUtilityButton("Close", new Vector2(0.07f, 0.04f), new Vector2(0.30f, 0.13f), ToggleTestMenu);
            CreateUtilityButton("Start Screen", new Vector2(0.34f, 0.04f), new Vector2(0.93f, 0.13f), OnReturnToStartScreen);
        }

        private void CreateSwitchButton(string label, string testId, Vector2 min, Vector2 max)
        {
            var go = UIStyleKit.MakeRect(_testSwitchPanel.transform, label, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(320, 90, 18,
                new Color(0.10f, 0.08f, 0.16f, 0.95f),
                UIStyleKit.Colors.BgCardBorder, 1);
            img.type = Image.Type.Sliced;
            var btn = go.AddComponent<Button>();
            UIStyleKit.MakeText(go.transform, "Label", Vector2.zero, Vector2.one,
                label, UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary);
            btn.onClick.AddListener(() => OnSwitchTest(testId));
        }

        private void CreateUtilityButton(string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = UIStyleKit.MakeRect(_testSwitchPanel.transform, label, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(280, 80, 18,
                new Color(0.12f, 0.06f, 0.10f, 0.95f),
                UIStyleKit.Colors.Danger, 1);
            img.type = Image.Type.Sliced;
            var btn = go.AddComponent<Button>();
            UIStyleKit.MakeText(go.transform, "Label", Vector2.zero, Vector2.one,
                label, UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary);
            btn.onClick.AddListener(action);
        }

        // ── Button callbacks ─────────────────────────────────────────────────────

        private void OnSkipTest()
        {
            if (_orchestrator == null) return;
            AudioFeedbackManager.Instance?.PlayError();
            _orchestrator.SkipCurrentTest();
        }

        private void OnMainMenu()
        {
            if (_orchestrator == null) return;
            AudioFeedbackManager.Instance?.PlayClick();
            ToggleTestMenu();
        }

        private void ToggleTestMenu()
        {
            if (_testSwitchPanel == null) return;
            _testSwitchPanel.SetActive(!_testSwitchPanel.activeSelf);
        }

        private void OnSwitchTest(string testId)
        {
            if (_orchestrator == null) return;
            AudioFeedbackManager.Instance?.PlayClick();
            _testSwitchPanel.SetActive(false);
            _orchestrator.SwitchToTest(testId);
        }

        private void OnReturnToStartScreen()
        {
            if (_orchestrator == null) return;
            AudioFeedbackManager.Instance?.PlayError();
            _testSwitchPanel.SetActive(false);
            _orchestrator.ReturnToMainMenu();
        }

        // ── Update helpers ───────────────────────────────────────────────────────

        private void UpdateSessionCounter()
        {
            if (_sessionCounterText == null || _orchestrator == null) return;
            int completed = _orchestrator.CompletedTestCount;
            int total = _orchestrator.TotalTestCount;
            _sessionCounterText.text = $"{completed + 1} of {total}";
        }

        private void UpdateProgress()
        {
            var test = _orchestrator.CurrentTest;
            if (test == null) return;

            // Simple progress heuristic
            float progress = 0;
            string label = "";

            if (test is OphthalSuite.Perimetry.PerimetryModule pm)
            {
                var master = FindFirstObjectByType<global::Perimetry.PerimetryMaster>();
                if (master != null)
                {
                    progress = 0.5f;
                    label = "Testing...";
                }
            }
            else
            {
                progress = 0.3f;
                label = test.DisplayName;
            }

            _progressFill.fillAmount = Mathf.Lerp(_progressFill.fillAmount, progress, Time.deltaTime * 2f);
            _counterText.text = $"{Mathf.RoundToInt(_progressFill.fillAmount * 100)}%";

            // Breathing animation on the progress ring
            float breathe = UIStyleKit.PulseSine(Time.time, 4f);
            _progressFill.color = Color.Lerp(UIStyleKit.Colors.ProgressFill, UIStyleKit.Colors.AccentBright, breathe * 0.3f);

            if (_instructionText != null)
                _instructionText.text = "";
        }

        private IEnumerator Fade(float targetAlpha)
        {
            bool showing = targetAlpha > 0.5f;
            _canvasGroup.blocksRaycasts = showing;
            _canvasGroup.interactable = showing;

            float startAlpha = _canvasGroup.alpha;
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, UIStyleKit.EaseInOut(t / fadeDuration));
                yield return null;
            }
            _canvasGroup.alpha = targetAlpha;
            _isVisible = showing;
            _canvasGroup.blocksRaycasts = showing;
            _canvasGroup.interactable = showing;
            _isFading = false;
        }
    }
}
