// SessionEndUI.cs
// Glassmorphism session summary screen shown after all tests complete.
// Displays per-test results with reliability badges and a "NEXT PATIENT" button.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OphthalSuite.Core
{
    public class SessionEndUI : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.6f;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GameObject _card;
        private Text _titleText;
        private Text _patientText;
        private Text _durationText;
        private Transform _resultsContainer;
        private AppOrchestrator _orchestrator;

        private bool _isShowing;

        private void Awake()
        {
            _orchestrator = FindFirstObjectByType<AppOrchestrator>();
            BuildUI();
            _canvas.enabled = false;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Show the session-end summary.</summary>
        public void Show(List<TestResult> results, SessionContext ctx, float durationSec)
        {
            if (_isShowing) return;
            _isShowing = true;

            try
            {
                _patientText.text = $"Patient: {ctx?.patientId ?? "—"}  •  Eye: {ctx?.eye ?? "—"}  •  Age: {ctx?.age ?? 0}";
                _durationText.text = $"Total Duration: {FormatTime(durationSec)}";

                // Clear old result cards
                foreach (Transform child in _resultsContainer)
                    Destroy(child.gameObject);

                // Build result cards
                if (results != null)
                {
                    foreach (var result in results)
                        CreateResultCard(result);
                }

                _canvas.enabled = true;
                _canvasGroup.alpha = 0;
                _card.transform.localScale = Vector3.one * 0.88f;
                StartCoroutine(AnimateIn());

                AudioFeedbackManager.Instance?.PlaySessionStart();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SessionEndUI Show error (skipped test?): {e.Message}");
            }
            finally
            {
                // This is the last block of the method, guaranteeing it triggers
                // even if the UI card creation fails above.
                string sid = ctx != null ? ctx.sessionId : "";
                var display = FindFirstObjectByType<OphthalSuite.Core.DiagnosisDisplay>();
                if (display != null)
                {
                    display.ShowForSession(sid);
                    // TASK 4: Move DiagnosisDisplay panel to the top of the UI hierarchy
                    display.transform.SetParent(_canvas.transform, false);
                    display.transform.SetAsLastSibling();
                }
            }
        }

        public void Hide()
        {
            if (!_isShowing) return;
            StartCoroutine(AnimateOut());
        }

        // ── Button callbacks ─────────────────────────────────────────────────────

        private void OnNextPatient()
        {
            AudioFeedbackManager.Instance?.PlayCorrect();
            _orchestrator?.PrepareNextPatient();
            Hide();
        }

        // ── UI Construction ──────────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasGo = new GameObject("SessionEndUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();

            VRCanvasUtil.ConfigureFloatingMonitorCanvas(_canvas, canvasGo, 110);
            canvasGo.AddComponent<GraphicRaycaster>();
            VRPointer.UpgradeCanvasForVR(canvasGo);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            // Dark overlay
            var bgGo = UIStyleKit.MakeRect(canvasGo.transform, "Overlay", Vector2.zero, Vector2.one);
            bgGo.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.06f, 0.88f);

            // Main card
            _card = UIStyleKit.MakeRect(canvasGo.transform, "Card", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            var cardImg = _card.AddComponent<Image>();
            cardImg.sprite = UIStyleKit.MakeRoundedRect(900, 700, 40,
                new Color(0.10f, 0.08f, 0.18f, 0.95f),
                new Color(0.3f, 0.8f, 0.5f, 0.3f), 2);
            cardImg.type = Image.Type.Sliced;

            // Title
            _titleText = UIStyleKit.MakeText(_card.transform, "Title",
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f),
                "SESSION COMPLETE", UIStyleKit.FontSize.Title, TextAnchor.MiddleCenter, UIStyleKit.Colors.Success);
            _titleText.fontStyle = FontStyle.Bold;

            // Checkmark icon
            var checkGo = UIStyleKit.MakeRect(_card.transform, "Check",
                new Vector2(0.42f, 0.80f), new Vector2(0.58f, 0.90f));
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.sprite = UIStyleKit.MakeCircleSprite(64, UIStyleKit.Colors.Success);
            checkImg.color = new Color(UIStyleKit.Colors.Success.r, UIStyleKit.Colors.Success.g,
                UIStyleKit.Colors.Success.b, 0.25f);
            UIStyleKit.MakeText(checkGo.transform, "Tick", Vector2.zero, Vector2.one,
                "✓", 28, TextAnchor.MiddleCenter, UIStyleKit.Colors.Success).fontStyle = FontStyle.Bold;

            // Patient info
            _patientText = UIStyleKit.MakeText(_card.transform, "Patient",
                new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.80f),
                "Patient: — • Eye: — • Age: —", UIStyleKit.FontSize.Body, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextPrimary);

            // Duration
            _durationText = UIStyleKit.MakeText(_card.transform, "Duration",
                new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.74f),
                "Total Duration: 0:00", UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, UIStyleKit.Colors.TextSecondary);

            // Separator
            var sepGo = UIStyleKit.MakeRect(_card.transform, "Sep",
                new Vector2(0.1f, 0.675f), new Vector2(0.9f, 0.68f));
            sepGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            // Results section label
            UIStyleKit.MakeText(_card.transform, "ResultsLabel",
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.67f),
                "TEST RESULTS", UIStyleKit.FontSize.Label, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextSecondary);

            // Scrollable results container
            var resultsAreaGo = UIStyleKit.MakeRect(_card.transform, "ResultsArea",
                new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.62f));
            _resultsContainer = resultsAreaGo.transform;

            // ── Action Buttons ───────────────────────────────────────────────────

            // NEXT PATIENT (primary)
            var nextGo = UIStyleKit.MakeRect(_card.transform, "NextBtn",
                new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.14f));
            var nextImg = nextGo.AddComponent<Image>();
            nextImg.sprite = UIStyleKit.MakeRoundedRect(700, 100, 28,
                UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.Success, 2);
            nextImg.type = Image.Type.Sliced;

            var nextBtn = nextGo.AddComponent<Button>();
            var colors = nextBtn.colors;
            colors.highlightedColor = UIStyleKit.Colors.BtnPrimaryHover;
            colors.pressedColor = UIStyleKit.Colors.BtnPrimaryPress;
            nextBtn.colors = colors;

            var nextLabel = UIStyleKit.MakeText(nextGo.transform, "Label",
                Vector2.zero, Vector2.one, "▶  NEXT PATIENT",
                UIStyleKit.FontSize.Button, TextAnchor.MiddleCenter, Color.white);
            nextLabel.fontStyle = FontStyle.Bold;

            nextBtn.onClick.AddListener(OnNextPatient);

            // Pulse animation on next patient button
            StartCoroutine(PulseButton(nextImg));
        }

        private void CreateResultCard(TestResult result)
        {
            int childCount = _resultsContainer.childCount;
            float cardHeight = 0.18f;
            float gap = 0.02f;
            float yTop = 1f - childCount * (cardHeight + gap);
            float yBot = yTop - cardHeight;

            if (yBot < -0.5f) return; // don't overflow too far

            var go = UIStyleKit.MakeRect(_resultsContainer, $"Result_{result.testId}",
                new Vector2(0.01f, yBot), new Vector2(0.99f, yTop));

            var bg = go.AddComponent<Image>();
            bg.sprite = UIStyleKit.MakeRoundedRect(600, 80, 15,
                new Color(0.08f, 0.06f, 0.15f, 0.9f),
                new Color(0.5f, 0.5f, 0.5f, 0.15f), 1);
            bg.type = Image.Type.Sliced;

            // Test name
            UIStyleKit.MakeText(go.transform, "Name",
                new Vector2(0.03f, 0.3f), new Vector2(0.40f, 0.85f),
                result.displayName ?? result.testId, UIStyleKit.FontSize.Small, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextPrimary);

            // Duration
            UIStyleKit.MakeText(go.transform, "Dur",
                new Vector2(0.03f, 0.0f), new Vector2(0.40f, 0.35f),
                $"{result.durationSeconds:F0}s", UIStyleKit.FontSize.Small, TextAnchor.MiddleLeft, UIStyleKit.Colors.TextMuted);

            // Category badge (Normal / Mild / Severe)
            string catText = string.IsNullOrEmpty(result.category) ? "Pending" : result.category;
            Color catColor = UIStyleKit.Colors.TextSecondary;
            if (catText == "Normal") catColor = UIStyleKit.Colors.Success;
            else if (catText == "Mild") catColor = UIStyleKit.Colors.Warning;
            else if (catText == "Severe") catColor = UIStyleKit.Colors.Danger;

            var catBadgeGo = UIStyleKit.MakeRect(go.transform, "CatBadge",
                new Vector2(0.42f, 0.2f), new Vector2(0.58f, 0.8f));
            var catBadgeImg = catBadgeGo.AddComponent<Image>();
            catBadgeImg.sprite = UIStyleKit.MakeRoundedRect(200, 50, 20,
                new Color(catColor.r * 0.2f, catColor.g * 0.2f, catColor.b * 0.2f, 0.9f),
                catColor, 2);
            catBadgeImg.type = Image.Type.Sliced;

            UIStyleKit.MakeText(catBadgeGo.transform, "CatBadgeText",
                Vector2.zero, Vector2.one,
                catText, UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, catColor);

            // Reliability badge
            Color badgeColor = GetReliabilityColor(result.reliabilityCategory);
            var badgeGo = UIStyleKit.MakeRect(go.transform, "Badge",
                new Vector2(0.6f, 0.2f), new Vector2(0.98f, 0.8f));
            var badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.sprite = UIStyleKit.MakeRoundedRect(200, 50, 20,
                new Color(badgeColor.r * 0.2f, badgeColor.g * 0.2f, badgeColor.b * 0.2f, 0.9f),
                badgeColor, 2);
            badgeImg.type = Image.Type.Sliced;

            UIStyleKit.MakeText(badgeGo.transform, "BadgeText",
                Vector2.zero, Vector2.one,
                result.reliabilityCategory ?? "—", UIStyleKit.FontSize.Small, TextAnchor.MiddleCenter, badgeColor);
        }

        private Color GetReliabilityColor(string category)
        {
            switch (category)
            {
                case "Acceptable":  return UIStyleKit.Colors.Success;
                case "Questionable": return UIStyleKit.Colors.Warning;
                case "Partial":     return new Color(0.4f, 0.7f, 1f); // blue
                case "Unreliable":  return UIStyleKit.Colors.Danger;
                default:            return UIStyleKit.Colors.TextSecondary;
            }
        }

        private string FormatTime(float seconds)
        {
            int min = Mathf.FloorToInt(seconds / 60f);
            int sec = Mathf.FloorToInt(seconds % 60f);
            return $"{min}:{sec:D2}";
        }

        // ── Animations ───────────────────────────────────────────────────────────

        private IEnumerator AnimateIn()
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float p = UIStyleKit.EaseOut(t / fadeDuration);
                _canvasGroup.alpha = p;
                _card.transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, UIStyleKit.Overshoot(Mathf.Clamp01(t / fadeDuration)));
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _card.transform.localScale = Vector3.one;
        }

        private IEnumerator AnimateOut()
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float p = UIStyleKit.EaseIn(t / fadeDuration);
                _canvasGroup.alpha = 1f - p;
                yield return null;
            }
            _canvasGroup.alpha = 0;
            _canvas.enabled = false;
            _isShowing = false;
        }

        private IEnumerator PulseButton(Image img)
        {
            while (true)
            {
                float p = UIStyleKit.PulseSine(Time.time, 2.2f);
                if (img != null)
                {
                    img.color = Color.Lerp(UIStyleKit.Colors.BtnPrimary, UIStyleKit.Colors.BtnPrimaryHover, p);
                    img.transform.localScale = Vector3.one * (1f + p * 0.025f);
                }
                yield return null;
            }
        }
    }
}
