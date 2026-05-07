// TestInstructionUI.cs
// Immersive, glassmorphism pre-test instruction overlay for DhrishtiLite.
// Shows before each test with clear instructions, a START button, and a SKIP button.
// The AppOrchestrator waits for the patient to either start or skip.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OphthalSuite.Core
{
    public class TestInstructionUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        // ── State ────────────────────────────────────────────────────────────────
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GameObject _card;
        private Text _testNameText;
        private Text _instructionText;
        private Text _testNumberText;
        private Image _iconRing;
        private Image _iconInner;
        private Button _startBtn;
        private Button _skipBtn;
        private Button _skipInstructionBtn;
        private Image _startBtnImg;
        private Coroutine _pulseCoroutine;
        private Coroutine _breatheCoroutine;

        /// <summary>True while the instruction screen is visible and awaiting input.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>After the instruction dismisses, true if the patient chose to skip the test.</summary>
        public bool WasSkipped { get; private set; }

        // ── Per-test instruction data ────────────────────────────────────────────
        private static readonly Dictionary<string, InstructionData> Instructions = new Dictionary<string, InstructionData>
        {
            { "PERIMETRY_24_2", new InstructionData
            {
                title = "Humphrey Perimetry 24-2",
                icon = "◉",
                instruction = "A small light will flash at different positions.\n\n" +
                              "• Keep looking at the center fixation dot\n" +
                              "• Press the trigger or tap anywhere when you see a flash\n" +
                              "• Some flashes are very faint — that's normal\n" +
                              "• Stay relaxed and respond when ready",
                color = new Color(0.4f, 0.7f, 1f)
            }},
            { "PELLI_ROBSON", new InstructionData
            {
                title = "Pelli-Robson Letter Test",
                icon = "A",
                instruction = "A row of letters will appear in each round.\n\n" +
                              "• Look at each letter in the row\n" +
                              "• Under every letter, tap the matching option\n" +
                              "• There are 8 rows in this test\n" +
                              "• Letters get fainter as you progress",
                color = new Color(0.9f, 0.6f, 1f)
            }},
            { "CSV_1000", new InstructionData
            {
                title = "CSV-1000 Letter Contrast",
                icon = "C",
                instruction = "A low-contrast letter will appear on screen.\n\n" +
                              "• Choose English or Kannada first\n" +
                              "• Look carefully at the displayed letter\n" +
                              "• Tap the matching option below\n" +
                              "• Tap None if you do not see a letter",
                color = new Color(0.5f, 0.9f, 0.7f)
            }},
            { "EDGE_DETECTION", new InstructionData
            {
                title = "Edge Detection — Tracing",
                icon = "✎",
                instruction = "A shape will appear with subtle edges.\n\n" +
                              "• Use your finger to trace the outline of the shape\n" +
                              "• An ink trail will follow your finger\n" +
                              "• Try to trace as close to the edges as possible\n" +
                              "• Tap 'Done' when finished, or 'Blank' if you see nothing",
                color = new Color(0.2f, 1f, 0.5f)
            }},
            { "PATTERN_DETECTION", new InstructionData
            {
                title = "Pattern Detection — Tracing",
                icon = "△",
                instruction = "Overlapping shapes will appear.\n\n" +
                              "• Use your finger to trace the outlines of the shapes\n" +
                              "• An ink trail will follow your finger\n" +
                              "• Trace along the edges of all overlapping shapes\n" +
                              "• Tap 'Done' when finished, or 'Blank' if you see nothing",
                color = new Color(1f, 0.65f, 0.2f)
            }},
            { "MOTION_DETECTION", new InstructionData
            {
                title = "Motion Detection",
                icon = "●→",
                instruction = "A ball will sweep across the screen.\n\n" +
                              "• Watch carefully for the moving ball\n" +
                              "• Tap ← if it moved left\n" +
                              "• Tap → if it moved right\n" +
                              "• Tap ✕ if you saw no motion",
                color = new Color(0.4f, 0.6f, 1f)
            }},
            { "SPARCS", new InstructionData
            {
                title = "SPARCS Spatial Awareness",
                icon = "⊞",
                instruction = "Dots will flash in one of four quadrants.\n\n" +
                              "• Keep looking at the center fixation point\n" +
                              "• After the flash, tap which quadrant showed the dots\n" +
                              "• ↖ ↗ ↙ ↘ represent the four quadrants\n" +
                              "• Tap ✕ if you saw nothing",
                color = new Color(0.8f, 0.5f, 1f)
            }}
        };

        // ── Unity lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            StartCoroutine(BuildWhenXRReady());
        }

        private IEnumerator BuildWhenXRReady()
        {
            const float maxWaitSeconds = 2f;
            float start = Time.realtimeSinceStartup;
            while (!XRSetup.IsInitialised && Time.realtimeSinceStartup - start < maxWaitSeconds)
                yield return null;

            EnsureBuilt();
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;
            BuildUI();
            _canvas.enabled = false;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Show the instruction screen for the given test module.
        /// The caller should yield/wait until IsShowing becomes false.
        /// After dismissal, check WasSkipped.
        /// </summary>
        public void ShowInstruction(ITestModule test, int testNumber, int totalTests)
        {
            EnsureBuilt();
            if (IsShowing) return;

            WasSkipped = false;
            IsShowing = true;

            // Populate content
            if (Instructions.TryGetValue(test.TestId, out var data))
            {
                _testNameText.text = data.title;
                _instructionText.text = data.instruction;
                _iconRing.color = data.color;
                _iconInner.color = new Color(data.color.r, data.color.g, data.color.b, 0.15f);
            }
            else
            {
                _testNameText.text = test.DisplayName;
                _instructionText.text = "Follow the on-screen instructions.";
                _iconRing.color = UIStyleKit.Colors.Accent;
                _iconInner.color = new Color(UIStyleKit.Colors.Accent.r, UIStyleKit.Colors.Accent.g, UIStyleKit.Colors.Accent.b, 0.15f);
            }

            _testNumberText.text = $"TEST {testNumber} OF {totalTests}";

            _canvas.enabled = true;
            _canvasGroup.alpha = 0;
            _card.transform.localScale = Vector3.one * 0.85f;

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseStartButton());

            if (_breatheCoroutine != null) StopCoroutine(_breatheCoroutine);
            _breatheCoroutine = StartCoroutine(BreatheIcon());

            StartCoroutine(AnimateIn());

            AudioFeedbackManager.Instance?.PlayClick();
        }

        /// <summary>
        /// Immediately hides the instruction screen (used when the orchestrator
        /// needs to force-close, e.g., session abort).
        /// </summary>
        public void ForceHide()
        {
            if (!IsShowing) return;
            StopAllCoroutines();
            _canvasGroup.alpha = 0;
            _canvas.enabled = false;
            IsShowing = false;
        }

        // ── Button callbacks ─────────────────────────────────────────────────────

        private void OnStartPressed()
        {
            if (!IsShowing) return;
            WasSkipped = false;
            AudioFeedbackManager.Instance?.PlayCorrect();
            StartCoroutine(AnimateOut());
        }

        private void OnSkipPressed()
        {
            if (!IsShowing) return;
            WasSkipped = true;
            AudioFeedbackManager.Instance?.PlayError();
            StartCoroutine(AnimateOut());
        }

        private void OnSkipInstructionPressed()
        {
            // Skip just the instruction, immediately start the test
            if (!IsShowing) return;
            WasSkipped = false;
            AudioFeedbackManager.Instance?.PlayClick();
            StartCoroutine(AnimateOut());
        }

        // ── Animations ───────────────────────────────────────────────────────────

        private IEnumerator AnimateIn()
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                float p = UIStyleKit.EaseOut(t / fadeInDuration);
                _canvasGroup.alpha = p;
                _card.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, UIStyleKit.Overshoot(Mathf.Clamp01(t / fadeInDuration)));
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _card.transform.localScale = Vector3.one;
        }

        private IEnumerator AnimateOut()
        {
            if (_pulseCoroutine != null) { StopCoroutine(_pulseCoroutine); _pulseCoroutine = null; }
            if (_breatheCoroutine != null) { StopCoroutine(_breatheCoroutine); _breatheCoroutine = null; }

            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float p = UIStyleKit.EaseIn(t / fadeOutDuration);
                _canvasGroup.alpha = 1f - p;
                _card.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, p);
                yield return null;
            }
            _canvasGroup.alpha = 0;
            _canvas.enabled = false;
            IsShowing = false;
        }

        private IEnumerator PulseStartButton()
        {
            while (true)
            {
                float p = UIStyleKit.PulseSine(Time.time, 2.2f);
                if (_startBtnImg != null)
                {
                    _startBtnImg.color = Color.Lerp(UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.BtnPrimaryHover, p);
                    _startBtnImg.transform.localScale = Vector3.one * (1f + p * 0.025f);
                }
                yield return null;
            }
        }

        private IEnumerator BreatheIcon()
        {
            while (true)
            {
                float p = UIStyleKit.PulseSine(Time.time, 3f);
                if (_iconRing != null)
                {
                    float scale = 1f + p * 0.06f;
                    _iconRing.transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }

        // ── UI Construction ──────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Root Canvas
            var canvasGo = new GameObject("InstructionUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();

            VRCanvasUtil.ConfigureFloatingMonitorCanvas(_canvas, canvasGo, 95);
            canvasGo.AddComponent<GraphicRaycaster>();
            VRPointer.UpgradeCanvasForVR(canvasGo);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            // Dark overlay background
            var bgGo = UIStyleKit.MakeRect(canvasGo.transform, "Overlay", Vector2.zero, Vector2.one);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.01f, 0.06f, 0.85f);

            // Ambient particles (subtle floating dots)
            CreateAmbientParticles(canvasGo.transform);

            // Main card with glassmorphism
            _card = UIStyleKit.MakeRect(canvasGo.transform, "Card", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));
            var cardImg = _card.AddComponent<Image>();
            cardImg.sprite = UIStyleKit.MakeRoundedRect(800, 600, 40,
                new Color(0.12f, 0.08f, 0.22f, 0.92f),
                new Color(0.6f, 0.4f, 0.9f, 0.3f), 2);
            cardImg.type = Image.Type.Sliced;

            // Inner glow border
            var glowGo = UIStyleKit.MakeRect(_card.transform, "Glow", new Vector2(-0.005f, -0.005f), new Vector2(1.005f, 1.005f));
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.sprite = UIStyleKit.MakeRoundedRect(810, 610, 42,
                Color.clear, new Color(0.6f, 0.3f, 1f, 0.12f), 3);
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;

            // Test number badge (top)
            _testNumberText = UIStyleKit.MakeText(_card.transform, "TestNumber",
                new Vector2(0.1f, 0.90f), new Vector2(0.9f, 0.97f),
                "TEST 1 OF 7", UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter,
                UIStyleKit.Colors.TextSecondary);
            _testNumberText.fontStyle = FontStyle.Bold;

            // Icon ring
            var iconGo = UIStyleKit.MakeRect(_card.transform, "IconRing",
                new Vector2(0.38f, 0.72f), new Vector2(0.62f, 0.90f));
            _iconRing = iconGo.AddComponent<Image>();
            _iconRing.sprite = UIStyleKit.MakeRingSprite(128, 6f, Color.white);
            _iconRing.color = UIStyleKit.Colors.Accent;
            _iconRing.raycastTarget = false;

            // Icon inner fill
            var iconInnerGo = UIStyleKit.MakeRect(iconGo.transform, "InnerFill",
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            _iconInner = iconInnerGo.AddComponent<Image>();
            _iconInner.sprite = UIStyleKit.MakeCircleSprite(64, Color.white);
            _iconInner.color = new Color(UIStyleKit.Colors.Accent.r, UIStyleKit.Colors.Accent.g, UIStyleKit.Colors.Accent.b, 0.15f);
            _iconInner.raycastTarget = false;

            // Test name heading
            _testNameText = UIStyleKit.MakeText(_card.transform, "TestName",
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.73f),
                "Test Name", UIStyleKit.FontSize.Heading, TextAnchor.MiddleCenter,
                UIStyleKit.Colors.TextTitle);
            _testNameText.fontStyle = FontStyle.Bold;

            // Accent line under title
            var lineGo = UIStyleKit.MakeRect(_card.transform, "AccentLine",
                new Vector2(0.35f, 0.615f), new Vector2(0.65f, 0.62f));
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.sprite = UIStyleKit.MakeGradient(
                new Color(UIStyleKit.Colors.Accent.r, UIStyleKit.Colors.Accent.g, UIStyleKit.Colors.Accent.b, 0f),
                UIStyleKit.Colors.Accent);
            lineImg.raycastTarget = false;

            // Instruction text
            _instructionText = UIStyleKit.MakeText(_card.transform, "Instructions",
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.60f),
                "Instructions here...", UIStyleKit.FontSize.Body, TextAnchor.UpperLeft,
                UIStyleKit.Colors.TextPrimary);
            _instructionText.lineSpacing = 1.3f;

            // ── Buttons ──────────────────────────────────────────────────────────

            // START TEST button (primary, large)
            var startGo = UIStyleKit.MakeRect(_card.transform, "StartBtn",
                new Vector2(0.12f, 0.08f), new Vector2(0.58f, 0.22f));
            _startBtnImg = startGo.AddComponent<Image>();
            _startBtnImg.sprite = UIStyleKit.MakeRoundedRect(400, 100, 25,
                UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.Accent, 2);
            _startBtnImg.type = Image.Type.Sliced;
            _startBtn = startGo.AddComponent<Button>();
            var startColors = _startBtn.colors;
            startColors.highlightedColor = UIStyleKit.Colors.BtnPrimaryHover;
            startColors.pressedColor = UIStyleKit.Colors.BtnPrimaryPress;
            _startBtn.colors = startColors;
            var startLabel = UIStyleKit.MakeText(startGo.transform, "Label",
                Vector2.zero, Vector2.one, "▶  START TEST",
                UIStyleKit.FontSize.Button, TextAnchor.MiddleCenter, Color.white);
            startLabel.fontStyle = FontStyle.Bold;
            _startBtn.onClick.AddListener(OnStartPressed);

            // SKIP TEST button (danger outlined)
            var skipGo = UIStyleKit.MakeRect(_card.transform, "SkipBtn",
                new Vector2(0.62f, 0.08f), new Vector2(0.88f, 0.22f));
            var skipImg = skipGo.AddComponent<Image>();
            skipImg.sprite = UIStyleKit.MakeRoundedRect(250, 100, 25,
                new Color(0.15f, 0.08f, 0.08f, 0.9f),
                UIStyleKit.Colors.Danger, 2);
            skipImg.type = Image.Type.Sliced;
            _skipBtn = skipGo.AddComponent<Button>();
            var skipColors = _skipBtn.colors;
            skipColors.highlightedColor = UIStyleKit.Colors.BtnDangerHover;
            skipColors.pressedColor = new Color(0.6f, 0.15f, 0.15f);
            _skipBtn.colors = skipColors;
            UIStyleKit.MakeText(skipGo.transform, "Label",
                Vector2.zero, Vector2.one, "SKIP TEST",
                UIStyleKit.FontSize.ButtonSm, TextAnchor.MiddleCenter, UIStyleKit.Colors.Danger);
            _skipBtn.onClick.AddListener(OnSkipPressed);

            // SKIP INSTRUCTION (small text button at bottom-right of card)
            var skipInstGo = UIStyleKit.MakeRect(_card.transform, "SkipInst",
                new Vector2(0.62f, 0.01f), new Vector2(0.92f, 0.07f));
            _skipInstructionBtn = skipInstGo.AddComponent<Button>();
            // No background image — text-only button
            var skipInstLabel = UIStyleKit.MakeText(skipInstGo.transform, "Label",
                Vector2.zero, Vector2.one, "skip instructions →",
                UIStyleKit.FontSize.Small, TextAnchor.MiddleRight, UIStyleKit.Colors.TextMuted);
            skipInstLabel.fontStyle = FontStyle.Italic;
            _skipInstructionBtn.onClick.AddListener(OnSkipInstructionPressed);
        }

        private void CreateAmbientParticles(Transform parent)
        {
            // Create a few subtle floating dots for atmosphere
            for (int i = 0; i < 12; i++)
            {
                float x = UnityEngine.Random.Range(0.05f, 0.95f);
                float y = UnityEngine.Random.Range(0.05f, 0.95f);
                float size = UnityEngine.Random.Range(0.008f, 0.02f);

                var dotGo = UIStyleKit.MakeRect(parent, $"Particle_{i}",
                    new Vector2(x, y), new Vector2(x + size, y + size));
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.sprite = UIStyleKit.MakeCircleSprite(16, Color.white);
                dotImg.color = UIStyleKit.Colors.ParticleDot;
                dotImg.raycastTarget = false;

                StartCoroutine(FloatParticle(dotGo.GetComponent<RectTransform>(), i));
            }
        }

        private IEnumerator FloatParticle(RectTransform rt, int seed)
        {
            if (rt == null) yield break;
            Vector2 origin = rt.anchoredPosition;
            float speed = 0.3f + seed * 0.08f;
            float amplitude = 8f + seed * 2f;

            while (true)
            {
                if (rt == null) yield break;
                float t = Time.time * speed;
                float ox = Mathf.Sin(t + seed * 1.3f) * amplitude;
                float oy = Mathf.Cos(t * 0.7f + seed * 0.9f) * amplitude * 0.6f;
                rt.anchoredPosition = origin + new Vector2(ox, oy);
                yield return null;
            }
        }

        // ── Data ─────────────────────────────────────────────────────────────────

        private struct InstructionData
        {
            public string title;
            public string icon;
            public string instruction;
            public Color color;
        }
    }
}
