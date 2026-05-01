// MainMenuUI.cs
// Data-driven home menu: builds buttons from OphthalTestCatalog only (no per-test logic).

using UnityEngine;
using UnityEngine.UI;

namespace OphthalSuite.Core
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private OphthalTestCatalog catalog;
        [SerializeField] private RectTransform buttonContainer;

        [Header("Optional")]
        [SerializeField] private bool loadCatalogFromResources = true;
        [SerializeField] private string resourcesCatalogName = "DefaultOphthalTestCatalog";

        private static Font BuiltinMenuFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        private void Start()
        {
            if (catalog == null && loadCatalogFromResources)
                catalog = Resources.Load<OphthalTestCatalog>(resourcesCatalogName);

            if (buttonContainer == null)
            {
                var go = new GameObject("ButtonList", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                buttonContainer = go.GetComponent<RectTransform>();
                buttonContainer.anchorMin = new Vector2(0.08f, 0.12f);
                buttonContainer.anchorMax = new Vector2(0.92f, 0.88f);
                buttonContainer.offsetMin = Vector2.zero;
                buttonContainer.offsetMax = Vector2.zero;
            }

            var vlg = buttonContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = buttonContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 12;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var fitter = buttonContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = buttonContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildButtons();
        }

        private void BuildButtons()
        {
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
                Destroy(buttonContainer.GetChild(i).gameObject);

            if (catalog == null || catalog.tests == null) return;

            foreach (var e in catalog.tests)
            {
                if (e == null || !e.enabled || string.IsNullOrEmpty(e.testId)) continue;
                CreateRow(e);
            }
        }

        private void CreateRow(OphthalTestCatalog.Entry e)
        {
            var go = new GameObject("Btn_" + e.testId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(buttonContainer, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 58;
            le.preferredHeight = 58;
            le.flexibleWidth = 1f;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.11f, 0.13f, 0.17f, 1f);

            var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(go.transform, false);
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(18, 6);
            tr.offsetMax = new Vector2(-18, -6);

            var font = BuiltinMenuFont();
            var txt = txtGo.GetComponent<Text>();
            if (font != null) txt.font = font;
            txt.fontSize = 17;
            txt.color = new Color(0.9f, 0.92f, 0.94f, 1f);
            txt.alignment = TextAnchor.MiddleLeft;
            txt.supportRichText = true;

            string title = string.IsNullOrEmpty(e.displayName) ? e.testId : e.displayName;
            txt.text = string.IsNullOrEmpty(e.subtitle)
                ? title
                : $"{title}\n<size=12><color=#7a7a88>{e.subtitle}</color></size>";

            string tid = e.testId;
            go.GetComponent<Button>().onClick.AddListener(() => OnPickTest(tid));
        }

        private void OnPickTest(string testId)
        {
            var flow = AppFlowCoordinator.Instance;
            if (flow == null)
            {
                Debug.LogWarning("MainMenuUI: AppFlowCoordinator not found.");
                return;
            }

            flow.StartSingleTest(flow.MenuDefaultPatientId, flow.MenuDefaultEye, flow.MenuDefaultAge, testId, loadExamScene: true);
        }
    }
}
