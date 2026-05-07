// TestTransitionUI.cs
// Inter-test "Rest" and "Skip" menu for DhrishtiLite.
// Shows celebration for completed tests, session progress, and allows
// skipping the rest period, next test, or current test.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace OphthalSuite.Core
{
    public class TestTransitionUI : MonoBehaviour
    {
        private AppOrchestrator _orchestrator;
        private CanvasGroup _canvasGroup;
        private GameObject _content;
        
        private Text _statusTitle;
        private Text _timerText;
        private Text _nextTestText;
        private Text _progressText;
        private Image _progressFill;
        private Image _progressTrack;
        
        [SerializeField] private float restDuration = 10f;
        [SerializeField] private float fadeDuration = 0.6f;
        
        private bool _isShowing = false;
        private Coroutine _timerCoroutine;
        private Coroutine _celebrationCoroutine;
        private int _completedTests = 0;
        private int _totalTests = 0;

        // Celebration particles
        private readonly List<RectTransform> _sparkles = new List<RectTransform>();
        private readonly List<Image> _sparkleImages = new List<Image>();

        private void Awake()
        {
            _orchestrator = FindFirstObjectByType<AppOrchestrator>();
            StartCoroutine(BuildWhenXRReady());
        }

        private IEnumerator BuildWhenXRReady()
        {
            const float maxWaitSeconds = 2f;
            float start = Time.realtimeSinceStartup;
            while (!XRSetup.IsInitialised && Time.realtimeSinceStartup - start < maxWaitSeconds)
                yield return null;

            BuildUI();
        }

        private void Start()
        {
            // Subscribe to orchestrator events if they exist
            // For now, we'll poll the state in Update or use a custom bridge
        }

        private void Update()
        {
            if (_canvasGroup == null) return;
            if (_orchestrator == null) return;

            // Simple logic: if a session is running but no test is currently active, show transition
            bool inTransition = _orchestrator.IsSessionRunning && _orchestrator.CurrentTest == null;

            if (inTransition && !_isShowing)
            {
                ShowTransition();
            }
            else if (!inTransition && _isShowing)
            {
                HideTransition();
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("TransitionUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;
            VRCanvasUtil.ConfigureFloatingMonitorCanvas(canvas, canvasGo, 1200);
            canvasGo.AddComponent<GraphicRaycaster>();
            VRPointer.UpgradeCanvasForVR(canvasGo);

            // Dark overlay
            var overlay = UIStyleKit.MakeRect(canvasGo.transform, "Overlay", Vector2.zero, Vector2.one);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0.02f, 0.01f, 0.06f, 0.7f);

            // Glassmorphism Card
            _content = UIStyleKit.MakeRect(canvasGo.transform, "Card", new Vector2(0.12f, 0.15f), new Vector2(0.88f, 0.85f));
            var img = _content.AddComponent<Image>();
            img.sprite = UIStyleKit.MakeRoundedRect(700, 500, 40,
                new Color(0.10f, 0.08f, 0.20f, 0.94f),
                new Color(0.5f, 0.8f, 0.4f, 0.3f), 2);
            img.type = Image.Type.Sliced;

            // Sparkle container (behind text, inside card)
            CreateSparkles(_content.transform);

            // 1. Celebration Icon
            var checkGo = UIStyleKit.MakeRect(_content.transform, "CheckIcon",
                new Vector2(0.38f, 0.78f), new Vector2(0.62f, 0.95f));
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.sprite = UIStyleKit.MakeCircleSprite(64, UIStyleKit.Colors.Success);
            checkImg.color = new Color(UIStyleKit.Colors.Success.r, UIStyleKit.Colors.Success.g,
                UIStyleKit.Colors.Success.b, 0.2f);

            var checkText = UIStyleKit.MakeText(checkGo.transform, "Tick",
                Vector2.zero, Vector2.one, "✓", 36, TextAnchor.MiddleCenter, UIStyleKit.Colors.Success);
            checkText.fontStyle = FontStyle.Bold;

            // 2. Completion Title
            _statusTitle = UIStyleKit.MakeText(_content.transform, "Title",
                new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.78f),
                "TEST COMPLETE ✓", UIStyleKit.FontSize.Heading, TextAnchor.MiddleCenter, UIStyleKit.Colors.Success);
            _statusTitle.fontStyle = FontStyle.Bold;

            // 3. Session Progress (e.g., "3 of 7 tests complete")
            _progressText = UIStyleKit.MakeText(_content.transform, "Progress",
                new Vector2(0.1f, 0.60f), new Vector2(0.9f, 0.68f),
                "3 of 7 tests complete", UIStyleKit.FontSize.Body, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary);

            // 4. Progress Bar
            var trackGo = UIStyleKit.MakeRect(_content.transform, "ProgressTrack",
                new Vector2(0.15f, 0.54f), new Vector2(0.85f, 0.58f));
            _progressTrack = trackGo.AddComponent<Image>();
            _progressTrack.sprite = UIStyleKit.MakeRoundedRect(500, 30, 15,
                UIStyleKit.Colors.ProgressTrack);
            _progressTrack.type = Image.Type.Sliced;

            var fillGo = UIStyleKit.MakeRect(trackGo.transform, "ProgressFill",
                Vector2.zero, new Vector2(0.5f, 1f));
            _progressFill = fillGo.AddComponent<Image>();
            _progressFill.sprite = UIStyleKit.MakeRoundedRect(500, 30, 15,
                UIStyleKit.Colors.ProgressFill);
            _progressFill.type = Image.Type.Sliced;

            // 5. Rest Timer
            _timerText = UIStyleKit.MakeText(_content.transform, "Timer",
                new Vector2(0.1f, 0.43f), new Vector2(0.9f, 0.53f),
                "Resting for 10s...", UIStyleKit.FontSize.Body, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextSecondary);

            // 6. Next Test Preview
            _nextTestText = UIStyleKit.MakeText(_content.transform, "Next",
                new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.43f),
                "Up next: Contrast Sensitivity", UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextMuted);

            // 7. Action Buttons
            CreateButtons(_content.transform);
        }

        private void CreateSparkles(Transform parent)
        {
            // Pre-create sparkle particles for celebration
            for (int i = 0; i < 16; i++)
            {
                float x = Random.Range(0.05f, 0.95f);
                float y = Random.Range(0.4f, 0.95f);
                float size = Random.Range(0.01f, 0.025f);

                var go = UIStyleKit.MakeRect(parent, $"Sparkle_{i}",
                    new Vector2(x, y), new Vector2(x + size, y + size));
                var sImg = go.AddComponent<Image>();
                sImg.sprite = UIStyleKit.MakeCircleSprite(16, Color.white);
                sImg.color = Color.clear;
                sImg.raycastTarget = false;

                _sparkles.Add(go.GetComponent<RectTransform>());
                _sparkleImages.Add(sImg);
            }
        }

        private void CreateButtons(Transform parent)
        {
            // SKIP REST (Start Next)
            var startNextGo = UIStyleKit.MakeRect(parent, "SkipRest", new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.24f));
            var startNextImg = startNextGo.AddComponent<Image>();
            startNextImg.sprite = UIStyleKit.MakeRoundedRect(300, 100, 25,
                UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.Accent, 2);
            startNextImg.type = Image.Type.Sliced;
            
            var startNextBtn = startNextGo.AddComponent<Button>();
            var colors = startNextBtn.colors;
            colors.highlightedColor = UIStyleKit.Colors.BtnPrimaryHover;
            colors.pressedColor = UIStyleKit.Colors.BtnPrimaryPress;
            startNextBtn.colors = colors;
            var nextLabel = UIStyleKit.MakeText(startNextGo.transform, "Label", Vector2.zero, Vector2.one,
                "▶  NEXT TEST", UIStyleKit.FontSize.ButtonSm, TextAnchor.MiddleCenter, Color.white);
            nextLabel.fontStyle = FontStyle.Bold;
            startNextBtn.onClick.AddListener(OnSkipRest);

            // SKIP NEXT TEST
            var skipNextGo = UIStyleKit.MakeRect(parent, "SkipTest", new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.24f));
            var skipNextImg = skipNextGo.AddComponent<Image>();
            skipNextImg.sprite = UIStyleKit.MakeRoundedRect(300, 100, 25,
                new Color(0.15f, 0.08f, 0.08f, 0.9f), UIStyleKit.Colors.Danger, 2);
            skipNextImg.type = Image.Type.Sliced;
            
            var skipNextBtn = skipNextGo.AddComponent<Button>();
            UIStyleKit.MakeText(skipNextGo.transform, "Label", Vector2.zero, Vector2.one,
                "SKIP THIS TEST", UIStyleKit.FontSize.ButtonSm, TextAnchor.MiddleCenter, UIStyleKit.Colors.Danger);
            skipNextBtn.onClick.AddListener(OnSkipNextTest);
        }

        private void ShowTransition()
        {
            _isShowing = true;
            _completedTests++;
            _statusTitle.text = "TEST COMPLETE ✓";
            _statusTitle.color = UIStyleKit.Colors.Success;

            // Update progress info
            if (_orchestrator != null && _orchestrator.CurrentSession != null)
            {
                // Count total tests from session
                _totalTests = Mathf.Max(_totalTests, _completedTests + 1);
            }
            if (_totalTests <= 0) _totalTests = 7;

            _progressText.text = $"{_completedTests} of {_totalTests} tests complete";

            // Update progress bar fill
            float progress = Mathf.Clamp01((float)_completedTests / _totalTests);
            var fillRT = _progressFill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(progress, 1f);

            // Color the progress bar green if near completion
            _progressFill.color = progress >= 0.85f ? UIStyleKit.Colors.ProgressDone : Color.white;
            
            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerCoroutine = StartCoroutine(RunRestTimer());

            if (_celebrationCoroutine != null) StopCoroutine(_celebrationCoroutine);
            _celebrationCoroutine = StartCoroutine(PlayCelebration());

            // Scale-bounce entrance
            _content.transform.localScale = Vector3.one * 0.88f;
            StartCoroutine(BounceIn());
            StartCoroutine(Fade(1));
            
            AudioFeedbackManager.Instance?.PlayCorrect();
        }

        private void HideTransition()
        {
            _isShowing = false;
            if (_celebrationCoroutine != null) { StopCoroutine(_celebrationCoroutine); _celebrationCoroutine = null; }
            StartCoroutine(Fade(0));
        }

        private IEnumerator BounceIn()
        {
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(0.88f, 1f, UIStyleKit.Overshoot(Mathf.Clamp01(t / 0.5f)));
                _content.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _content.transform.localScale = Vector3.one;
        }

        private IEnumerator PlayCelebration()
        {
            // Animate sparkles bursting out
            var sparkleColors = new[] {
                new Color(0.4f, 1f, 0.6f, 1f),     // green
                new Color(0.6f, 0.4f, 1f, 1f),       // purple
                new Color(1f, 0.8f, 0.3f, 1f),       // gold
                new Color(0.3f, 0.8f, 1f, 1f)        // blue
            };

            for (int i = 0; i < _sparkles.Count; i++)
            {
                _sparkleImages[i].color = sparkleColors[i % sparkleColors.Length];
            }

            float duration = 2.5f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float normalised = t / duration;

                for (int i = 0; i < _sparkles.Count; i++)
                {
                    float delay = i * 0.08f;
                    float localT = Mathf.Clamp01((t - delay) / (duration - delay));

                    if (localT <= 0f)
                    {
                        _sparkleImages[i].color = new Color(_sparkleImages[i].color.r, _sparkleImages[i].color.g, _sparkleImages[i].color.b, 0f);
                        continue;
                    }

                    // Burst up then float down
                    float alpha = localT < 0.3f ? localT / 0.3f : 1f - (localT - 0.3f) / 0.7f;
                    alpha = Mathf.Clamp01(alpha) * 0.8f;
                    var c = _sparkleImages[i].color;
                    _sparkleImages[i].color = new Color(c.r, c.g, c.b, alpha);

                    // Float upward and wobble
                    float yOff = localT * 40f;
                    float xOff = Mathf.Sin(localT * 8f + i * 1.5f) * 12f;
                    _sparkles[i].anchoredPosition = new Vector2(
                        _sparkles[i].anchoredPosition.x + xOff * Time.deltaTime,
                        _sparkles[i].anchoredPosition.y + yOff * Time.deltaTime * 0.3f
                    );
                }
                yield return null;
            }

            // Fade all sparkles out
            for (int i = 0; i < _sparkleImages.Count; i++)
            {
                var c = _sparkleImages[i].color;
                _sparkleImages[i].color = new Color(c.r, c.g, c.b, 0f);
            }
        }

        private IEnumerator RunRestTimer()
        {
            float remaining = restDuration;
            while (remaining > 0)
            {
                _timerText.text = $"Rest your eyes for {Mathf.CeilToInt(remaining)}s...";
                remaining -= Time.deltaTime;
                yield return null;
            }
            
            // Auto-advance is handled by AppOrchestrator moving to next test
            _timerText.text = "Starting next test...";
        }

        private void OnSkipRest()
        {
            if (_orchestrator != null)
                _orchestrator.SkipCurrentRest();
                
            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerText.text = "Starting next...";
            AudioFeedbackManager.Instance?.PlayClick();
        }

        private void OnSkipNextTest()
        {
            if (_orchestrator != null)
                _orchestrator.SkipNextTest();

            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerText.text = "Skipping test...";
            AudioFeedbackManager.Instance?.PlayError();
        }

        private IEnumerator Fade(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, UIStyleKit.EaseInOut(t / fadeDuration));
                yield return null;
            }
            _canvasGroup.alpha = targetAlpha;
        }
    }
}
