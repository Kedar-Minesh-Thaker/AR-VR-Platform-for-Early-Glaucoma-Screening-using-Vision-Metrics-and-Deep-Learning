// SceneSetup.cs
// Unity Editor script — one-click scene setup for the OphthalSuite.
//
// Menu: OphthalSuite → Setup Scene
//       OphthalSuite → Setup VR Scene (Quest 2)
//
// This script creates all required GameObjects, wires references,
// and configures the scene for the perimetry integration layer.
// Run it on the SampleScene (or any empty scene).

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using Perimetry;
using OphthalSuite.Core;
using OphthalSuite.Perimetry;
using OphthalSuite.ContrastSensitivity.Csv1000;
using OphthalSuite.ContrastSensitivity.PelliRobson;
using OphthalSuite.ClinicalTests.Sparcs;
using OphthalSuite.ClinicalTests.MotionDetection;
using OphthalSuite.ClinicalTests.EdgeDetection;
using OphthalSuite.ClinicalTests.PatternDetection;

namespace OphthalSuite.Editor
{
    public static class SceneSetup
    {
        [MenuItem("OphthalSuite/Setup Scene")]
        public static void SetupScene()
        {
            // ── 1. Perimetry Bowl ────────────────────────────────────────────────
            GameObject bowl = GameObject.Find("PerimetryBowl");
            if (bowl == null)
            {
                bowl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bowl.name = "PerimetryBowl";
                bowl.transform.position = Vector3.zero;
                bowl.transform.localScale = new Vector3(12f, 12f, 12f);

                // Create black inside-facing material
                var bowlMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bowlMat.name = "BowlInsideMaterial";
                bowlMat.SetColor("_BaseColor", Color.black);
                // Render back faces only (inside of sphere)
                bowlMat.SetFloat("_CullMode", 1f);  // 1 = Front → shows back faces
                bowlMat.SetInt("_Cull", 1);
                bowl.GetComponent<Renderer>().material = bowlMat;

                // Remove collider (not needed)
                var col = bowl.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                Debug.Log("SceneSetup: Created PerimetryBowl (scale 12).");
            }

            // ── 2. Main Camera ───────────────────────────────────────────────────
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = Color.black;
                Debug.Log("SceneSetup: Created Main Camera.");
            }
            mainCam.transform.position = Vector3.zero;
            mainCam.transform.rotation = Quaternion.identity;
            mainCam.nearClipPlane = 0.01f;
            mainCam.farClipPlane = 100f;

            // Add GyroControl
            var gyro = mainCam.GetComponent<GyroControl>();
            if (gyro == null) gyro = mainCam.gameObject.AddComponent<GyroControl>();

            // Add FixationMonitor
            var fixMon = mainCam.GetComponent<FixationMonitor>();
            if (fixMon == null) fixMon = mainCam.gameObject.AddComponent<FixationMonitor>();

            // Add PatientResponse
            var patResp = mainCam.GetComponent<PatientResponse>();
            if (patResp == null) patResp = mainCam.gameObject.AddComponent<PatientResponse>();

            // ── 3. Fixation Dot (child of camera) ────────────────────────────────
            Transform fixDot = mainCam.transform.Find("FixationDot");
            if (fixDot == null)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "FixationDot";
                dot.transform.SetParent(mainCam.transform, false);
                dot.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                dot.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

                // Emissive white material
                var dotMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                dotMat.name = "FixationDotMaterial";
                dotMat.SetColor("_BaseColor", Color.black);
                dotMat.EnableKeyword("_EMISSION");
                dotMat.SetColor("_EmissionColor", Color.white * 2f);
                dot.GetComponent<Renderer>().material = dotMat;

                // Remove collider
                var dotCol = dot.GetComponent<Collider>();
                if (dotCol != null) Object.DestroyImmediate(dotCol);

                Debug.Log("SceneSetup: Created FixationDot.");
            }

            // ── 4. Stimulus Prefab ───────────────────────────────────────────────
            // Check if prefab already exists
            GameObject stimPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/StimulusPrefab.prefab");

            if (stimPrefab == null)
            {
                // Create in-scene first, then save as prefab
                GameObject stim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stim.name = "StimulusPrefab";
                stim.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

                // Add StimulusController
                stim.AddComponent<StimulusController>();

                // Emissive material
                var stimMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                stimMat.name = "StimulusMaterial";
                stimMat.SetColor("_BaseColor", Color.black);
                stimMat.EnableKeyword("_EMISSION");
                stim.GetComponent<Renderer>().material = stimMat;

                // Remove collider
                var stimCol = stim.GetComponent<Collider>();
                if (stimCol != null) Object.DestroyImmediate(stimCol);

                // Save as prefab
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                stimPrefab = PrefabUtility.SaveAsPrefabAsset(stim, "Assets/Prefabs/StimulusPrefab.prefab");
                Object.DestroyImmediate(stim);  // remove scene instance

                Debug.Log("SceneSetup: Created StimulusPrefab at Assets/Prefabs/.");
            }

            // ── 5. SharedDoctorMirror (singleton) ────────────────────────────────
            SharedDoctorMirror mirror = Object.FindFirstObjectByType<SharedDoctorMirror>();
            if (mirror == null)
            {
                var mirrorGo = new GameObject("SharedDoctorMirror");
                mirror = mirrorGo.AddComponent<SharedDoctorMirror>();

                // Create MonitorFeed RenderTexture if needed
                RenderTexture monitorFeed = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    "Assets/MonitorFeed.renderTexture");
                if (monitorFeed != null)
                    mirror.MonitorFeed = monitorFeed;
                // else SharedDoctorMirror auto-creates one at runtime

                Debug.Log("SceneSetup: Created SharedDoctorMirror.");
            }

            // ── 6. PerimetryMaster + PerimetryModule ─────────────────────────────
            PerimetryMaster master = Object.FindFirstObjectByType<PerimetryMaster>();
            GameObject perimetryGo;
            if (master == null)
            {
                perimetryGo = new GameObject("PerimetryTest");
                master = perimetryGo.AddComponent<PerimetryMaster>();
                Debug.Log("SceneSetup: Created PerimetryTest GameObject with PerimetryMaster.");
            }
            else
            {
                perimetryGo = master.gameObject;
            }

            var perimModule = perimetryGo.GetComponent<PerimetryModule>();
            if (perimModule == null)
            {
                perimModule = perimetryGo.AddComponent<PerimetryModule>();
                Debug.Log("SceneSetup: Added PerimetryModule adapter.");
            }

            // Wire PerimetryMaster Inspector references
            var masterSO = new SerializedObject(master);
            SetSerializedRef(masterSO, "stimulusPrefab", stimPrefab);
            SetSerializedRef(masterSO, "gyroControl", gyro);
            SetSerializedRef(masterSO, "fixationMonitor", fixMon);
            SetSerializedRef(masterSO, "patientResponse", patResp);
            var dmProp = masterSO.FindProperty("doctorMirror");
            if (dmProp != null) dmProp.objectReferenceValue = null;
            masterSO.ApplyModifiedProperties();

            // ── 6b. All other test modules ───────────────────────────────────────
            var allModules = new List<MonoBehaviour>();
            allModules.Add(perimModule);

            allModules.Add(EnsureTestModule<Csv1000Module>("CSV1000_Test"));
            allModules.Add(EnsureTestModule<PelliRobsonModule>("PelliRobson_Test"));
            allModules.Add(EnsureTestModule<SparcsModule>("SPARCS_Test"));
            allModules.Add(EnsureTestModule<MotionDetectionModule>("MotionDetection_Test"));
            allModules.Add(EnsureTestModule<EdgeDetectionModule>("EdgeDetection_Test"));
            allModules.Add(EnsureTestModule<PatternDetectionModule>("PatternDetection_Test"));

            // ── 7. AppOrchestrator ───────────────────────────────────────────────
            AppOrchestrator orchestrator = Object.FindFirstObjectByType<AppOrchestrator>();
            if (orchestrator == null)
            {
                var orchGo = new GameObject("AppOrchestrator");
                orchestrator = orchGo.AddComponent<AppOrchestrator>();
                Debug.Log("SceneSetup: Created AppOrchestrator.");
            }

            // Add SessionStartUI if not present
            if (orchestrator.GetComponent<SessionStartUI>() == null)
            {
                orchestrator.gameObject.AddComponent<SessionStartUI>();
                Debug.Log("SceneSetup: Added SessionStartUI to AppOrchestrator.");
            }

            // Wire ALL test modules into the testModules list
            var orchSO = new SerializedObject(orchestrator);
            var testModulesProp = orchSO.FindProperty("testModules");
            if (testModulesProp != null)
            {
                // Clear and re-add all modules to ensure correct order
                testModulesProp.ClearArray();
                for (int i = 0; i < allModules.Count; i++)
                {
                    testModulesProp.InsertArrayElementAtIndex(i);
                    testModulesProp.GetArrayElementAtIndex(i).objectReferenceValue = allModules[i];
                }
                Debug.Log($"SceneSetup: Wired {allModules.Count} test modules into AppOrchestrator.");
            }
            orchSO.ApplyModifiedProperties();

            // ── 8. Remove old DoctorMirror if present ────────────────────────────
            DoctorMirror oldMirror = Object.FindFirstObjectByType<DoctorMirror>();
            if (oldMirror != null)
            {
                Debug.Log("SceneSetup: Found old DoctorMirror — disabling it (SharedDoctorMirror replaces it).");
                oldMirror.enabled = false;
            }

            // ── Done ─────────────────────────────────────────────────────────────
            EditorUtility.DisplayDialog(
                "OphthalSuite — Scene Setup Complete",
                "All GameObjects created and wired:\n\n" +
                "• PerimetryBowl (scale 12)\n" +
                "• Main Camera + GyroControl + FixationMonitor + PatientResponse\n" +
                "• FixationDot (child of camera)\n" +
                "• StimulusPrefab (saved to Assets/Prefabs/)\n" +
                "• SharedDoctorMirror (singleton, WS port 8765)\n" +
                "• PerimetryTest (PerimetryMaster + PerimetryModule)\n" +
                "• AppOrchestrator (PerimetryModule wired)\n\n" +
                "To start a session at runtime:\n" +
                "  FindFirstObjectByType<AppOrchestrator>()\n" +
                "    .StartSession(\"P001\", \"OD\", 65);",
                "OK");

            // Mark scene dirty so user is prompted to save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("SceneSetup: ✓ Scene setup complete.");
        }

        /// <summary>Sets up the scene for Meta Quest 2 VR. Runs standard setup first, then adds Quest VR components.</summary>
        [MenuItem("OphthalSuite/Setup VR Scene (Quest 2)")]
        public static void SetupVRSceneQuest()
        {
            // Run the standard scene setup first
            SetupScene();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("SetupVRScene: No Main Camera — run standard Setup Scene first.");
                return;
            }

            // ── 1. XRSetup (runtime Quest bootstrap) ────────────────────────────
            // Quest uses the Oculus XR Plugin; XRSetup handles loader init.
            if (Object.FindFirstObjectByType<XRSetup>() == null)
            {
                var setupGo = new GameObject("XRSetup");
                setupGo.AddComponent<XRSetup>();
                Debug.Log("SetupVRScene: Created XRSetup (Quest 2 bootstrap).");
            }

            // ── 2. EyeOccluder on camera ────────────────────────────────────────
            if (mainCam.GetComponent<EyeOccluder>() == null)
            {
                mainCam.gameObject.AddComponent<EyeOccluder>();
                Debug.Log("SetupVRScene: Added EyeOccluder to Main Camera.");
            }

            // ── 3. AudioFeedbackManager ─────────────────────────────────────────
            if (Object.FindFirstObjectByType<AudioFeedbackManager>() == null)
            {
                var audioGo = new GameObject("AudioFeedbackManager");
                audioGo.AddComponent<AudioFeedbackManager>();
                Debug.Log("SetupVRScene: Created AudioFeedbackManager.");
            }

            // ── 4. DatabaseManager ──────────────────────────────────────────────
            if (Object.FindFirstObjectByType<Database.DatabaseManager>() == null)
            {
                var dbGo = new GameObject("DatabaseManager");
                dbGo.AddComponent<Database.DatabaseManager>();
                Debug.Log("SetupVRScene: Created DatabaseManager.");
            }

            // ── 5. EventSystem (for world-space UI in VR) ───────────────────────
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                Debug.Log("SetupVRScene: Created EventSystem.");
            }

            // ── Done ────────────────────────────────────────────────────────────
            EditorUtility.DisplayDialog(
                "OphthalSuite — Quest 2 VR Setup Complete",
                "VR components added:\n\n" +
                "• XRSetup (Quest stereo + 6DOF tracking, 90Hz, FFR OFF)\n" +
                "• EyeOccluder (digital monocular occlusion)\n" +
                "• AudioFeedbackManager (click & alert sounds)\n" +
                "• DatabaseManager (local SQLite storage)\n" +
                "• EventSystem (UI interaction)\n\n" +
                "REQUIRED — do these before building:\n\n" +
                "1. Edit → Project Settings → XR Plug-in Management\n" +
                "   → Android tab → check 'Oculus'\n" +
                "   → Uncheck 'Initialize XR on Startup'\n\n" +
                "2. Player Settings → Android:\n" +
                "   • Graphics API: OpenGLES3 (or Vulkan)\n" +
                "   • Minimum API: 29 (Android 10)\n" +
                "   • Scripting Backend: IL2CPP\n" +
                "   • Target Arch: ARM64\n" +
                "   • Texture Compression: ASTC\n\n" +
                "Patient input: Quest controller trigger or A button.\n" +
                "Non-tested eye auto-occluded via EyeOccluder.",
                "OK");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("SetupVRScene: ✓ Quest 2 VR scene setup complete.");
        }

        /// <summary>Legacy Cardboard setup — kept for reference but Quest 2 is now primary.</summary>
        [MenuItem("OphthalSuite/Setup VR Scene (Cardboard — Legacy)")]
        public static void SetupVRScene()
        {
            // Run the standard scene setup first
            SetupScene();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("SetupVRScene: No Main Camera — run standard Setup Scene first.");
                return;
            }

            // ── 1. XRSetup (runtime Cardboard bootstrap) ────────────────────────
            if (Object.FindFirstObjectByType<XRSetup>() == null)
            {
                var setupGo = new GameObject("XRSetup");
                setupGo.AddComponent<XRSetup>();
                Debug.Log("SetupVRScene: Created XRSetup (Cardboard bootstrap).");
            }

            // ── 2. EyeOccluder on camera ────────────────────────────────────────
            if (mainCam.GetComponent<EyeOccluder>() == null)
            {
                mainCam.gameObject.AddComponent<EyeOccluder>();
                Debug.Log("SetupVRScene: Added EyeOccluder to Main Camera.");
            }

            // ── 3. EventSystem (for world-space UI in VR) ───────────────────────
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                Debug.Log("SetupVRScene: Created EventSystem.");
            }

            // ── Done ────────────────────────────────────────────────────────────
            EditorUtility.DisplayDialog(
                "OphthalSuite — Cardboard VR Setup (Legacy)",
                "This is the legacy Cardboard setup.\n" +
                "For Quest 2, use OphthalSuite → Setup VR Scene (Quest 2).\n\n" +
                "VR components added:\n\n" +
                "• XRSetup (Cardboard stereo + barrel distortion + gyro)\n" +
                "• EyeOccluder (digital monocular occlusion)\n" +
                "• EventSystem (UI interaction)",
                "OK");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("SetupVRScene: ✓ Cardboard VR scene setup complete (legacy).");
        }

        /// <summary>Creates a screen-space canvas with MainMenuUI (catalog-driven buttons). Add EventSystem if missing.</summary>
        [MenuItem("OphthalSuite/Create Main Menu Canvas")]
        public static void CreateMainMenuCanvas()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.05f, 0.055f, 0.07f, 1f);

            var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
            title.transform.SetParent(canvasGo.transform, false);
            var tRt = title.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.08f, 0.88f);
            tRt.anchorMax = new Vector2(0.92f, 0.97f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            var tTxt = title.GetComponent<Text>();
            var menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                           ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (menuFont != null) tTxt.font = menuFont;
            tTxt.fontSize = 22;
            tTxt.color = new Color(0.75f, 0.88f, 0.92f);
            tTxt.text = "OphthalSuite — select examination";
            tTxt.alignment = TextAnchor.MiddleLeft;

            canvasGo.AddComponent<MainMenuUI>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            Selection.activeGameObject = canvasGo;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("OphthalSuite: MainMenuCanvas created. Assign OphthalTestCatalog on MainMenuUI or rely on Resources/DefaultOphthalTestCatalog.");
        }

        /// <summary>DontDestroyOnLoad root for doctor commands and scene switching (place in first loaded scene).</summary>
        [MenuItem("OphthalSuite/Create App Flow Root")]
        public static void CreateAppFlowRoot()
        {
            if (Object.FindFirstObjectByType<AppFlowCoordinator>() != null)
            {
                EditorUtility.DisplayDialog("App Flow Root", "An AppFlowCoordinator already exists in this scene.", "OK");
                return;
            }

            var go = new GameObject("AppFlowRoot");
            go.AddComponent<AppFlowCoordinator>();
            go.AddComponent<DoctorInboundProcessor>();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("OphthalSuite: AppFlowRoot created. Set Main Menu / Patient Exam scene names on AppFlowCoordinator. Add SharedDoctorMirror in this or the same first scene.");
        }

        // Helper: set a serialized field by name
        private static void SetSerializedRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        /// <summary>Find or create a GameObject with the given component.</summary>
        private static T EnsureTestModule<T>(string goName) where T : MonoBehaviour
        {
            T existing = Object.FindFirstObjectByType<T>();
            if (existing != null) return existing;

            var go = new GameObject(goName);
            var comp = go.AddComponent<T>();
            Debug.Log($"SceneSetup: Created {goName} with {typeof(T).Name}.");
            return comp;
        }
    }
}
#endif
