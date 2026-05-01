# OphthalSuite — Unified Unity URP Clinical Test App

Research-oriented ophthalmic test battery for **Android (patient phone)** and **laptop clinician view**. This README describes the **final integrated layout (Part 4)**, folder structure, setup, and how components connect.

> **Not a medical device.** For investigation and education only.

---

## 1. Folder structure (Unity scripts)

```
Assets/Scripts/
├── Core/                          # Shared orchestration & doctor stream
│   ├── App/                       # Unified app flow (menu + doctor commands)
│   │   ├── AppFlowCoordinator.cs  # Scene load MainMenu ↔ PatientExam, doctor commands
│   │   ├── DoctorInboundProcessor.cs # WS → HandleDoctorCommand (main thread)
│   │   ├── MainMenuUI.cs          # Catalog-driven home menu buttons
│   │   └── OphthalTestCatalog.cs  # ScriptableObject: testId / label / enabled
│   ├── Networking/
│   │   ├── WsCodec.cs             # WebSocket frame parse (inbound doctor JSON)
│   │   └── DoctorCommandModels.cs # DOCTOR_COMMAND / PATIENT_COMMAND_ACK JSON
│   ├── AppOrchestrator.cs       # Session, CSV log, trial routing, TRIAL_OVERLAY
│   ├── SharedDoctorMirror.cs    # WebSocket + JPEG mirror (phone → LAN)
│   ├── SharedSchema.cs          # SessionContext, TestTrialEvent, TestResult, …
│   ├── SessionStartUI.cs        # On-phone session start (IMGUI)
│   ├── ITestModule.cs
│   ├── DoctorStream/
│   │   └── DoctorStreamModels.cs  # STIMULUS_STATE, TRIAL_OVERLAY, …
│   ├── VisualTests/               # IVisualTestModule, descriptors
│   ├── Management/
│   │   ├── VisualTestManager.cs   # Register test roots, LoadTest / unload
│   │   └── UnifiedTestManager.cs  # Part 4: IDs, full vs single-test session
│   ├── Input/                     # PatientInputHub (optional)
│   └── Logging/                   # ClinicalLogModels (optional export)
├── Perimetry/                     # 24-2 bowl VF (existing + Part 4 HUD / SITA options)
├── ContrastSensitivity/           # CSV-1000, Pelli-Robson + common staircase utils
└── ClinicalTests/                 # SPARCS, Motion, Edge, Pattern + ClinicalUiKit

ClinicalViewer/                    # Laptop “stream layer”
├── unified-server.js              # WS (Unity) ↔ Node; SSE + POST /api/command → Unity
└── unified-dashboard.html         # Mirror + metadata + **Exam control** panel
```

Supporting docs: `INTEGRATION.md` (WiFi, message reference, doctor POST API). This README is the **primary setup guide**.

---

## 2. Unified scenes (menu + exam)

Use **two patient scenes** plus whatever you already use for the laptop (HTML dashboard is served by Node, not Unity).

| Scene (name in Build Settings) | Typical contents |
|--------------------------------|------------------|
| **MainMenu** | Dark UI: **OphthalSuite → Create Main Menu Canvas**. **AppFlowRoot** (**OphthalSuite → Create App Flow Root**): `AppFlowCoordinator` + `DoctorInboundProcessor`. **`SharedDoctorMirror`** (singleton, WS 8765). Optional title / branding. |
| **PatientExam** | Full test rig: bowl, camera, gyro, **`VisualTestManager`** (all module roots), **`AppOrchestrator`**, per-test objects, `MonitorFeed` assigned on mirror. |

On **`AppFlowCoordinator`**, set **Main Menu Scene Name** = `MainMenu` and **Patient Exam Scene Name** = `PatientExam` (must match Build Settings exactly).

**Build order:** index `0` = **MainMenu** (or a tiny bootstrap scene that only contains DDOL objects then loads MainMenu). The coordinator and mirror use **`DontDestroyOnLoad`** when placed in the first loaded scene.

**Default test list asset:** `Assets/Resources/DefaultOphthalTestCatalog.asset` — seven tests, toggle **`enabled`** per row to disable a module in the menu without code changes.

---

## 3. Pasting into an existing Unity project

1. Copy the folders above into your project’s `Assets/Scripts/` (and `ClinicalViewer/` at repo root or any path you prefer).
2. Ensure **URP**, **Unity UI (uGUI)**, and **Input** (legacy `Input` API used by `PatientResponse`) are available.
3. Open Unity and let scripts compile; resolve any missing references (e.g. assign fonts in modules if desired).
4. Add an **EventSystem** to UI scenes if you use **Button**s (contrast / clinical tests / main menu).
5. Add **MainMenu** and **PatientExam** to **File → Build Settings**; put **MainMenu** at index **0** for app launch.

---

## 4. GameObjects & Inspector wiring

### Required scene objects

| Object | Components | Notes |
|--------|------------|--------|
| **SharedDoctorMirror** | `SharedDoctorMirror` | Singleton; optional `MonitorFeed` RT; WebSocket port (default 8765). Inbound doctor JSON is handled by **`DoctorInboundProcessor`**. |
| **AppFlowCoordinator** | `AppFlowCoordinator` | First scene (often **MainMenu**); **DontDestroyOnLoad**; **main / exam scene names** must match Build Settings. |
| **DoctorInboundProcessor** | `DoctorInboundProcessor` | Keep with coordinator (or any DDOL object); applies **`DOCTOR_COMMAND`** on the main thread. |
| **AppOrchestrator** | `AppOrchestrator` | Usually **PatientExam**; wire **Visual Test Manager** OR legacy `testModules` list. |
| **VisualTestManager** | `VisualTestManager` | **Tests**: one entry per module root (see table below). Disable all roots except helpers; use **Activate Tests Via Manager** on orchestrator if only one test active at a time. |
| **UnifiedTestManager** (optional) | `UnifiedTestManager` | Assign same **App Orchestrator** + **Visual Test Manager**. Use for API / future UI. |
| **SessionStartUI** | `SessionStartUI` | Same object as orchestrator or any; finds `AppOrchestrator`. |
| **MainMenuUI** | `MainMenuUI` | **MainMenu** canvas; optional **OphthalTestCatalog** (else **`Resources/DefaultOphthalTestCatalog`**). |

### Test module roots (each is a GameObject with the module script)

| TestId | Typical component | Notes |
|--------|-------------------|--------|
| `PERIMETRY_24_2` | `PerimetryMaster` + `PerimetryModule` | Clear legacy **DoctorMirror** ref on master; use SharedDoctorMirror only. |
| `CSV_1000` | `Csv1000Module` | Self-builds UI. |
| `PELLI_ROBSON` | `PelliRobsonModule` | Assign Kannada-capable **Font** for script modes using Kannada. |
| `SPARCS` | `SparcsModule` | Optional **Fixation Monitor**; bowl auto-created or use existing scene bowl. |
| `MOTION_DETECTION` | `MotionDetectionModule` | |
| `EDGE_DETECTION` | `EdgeDetectionModule` | |
| `PATTERN_DETECTION` | `PatternDetectionModule` | |

**Perimetry (Part 4)**

- **Progress HUD**: `PerimetryModule` builds a small top-left overlay (`0/N` then `k/N` each trial).
- **SITA-like options** (on `PerimetryMaster`):
  - **Sita Like Presentation Order** — eccentricity-binned ordering (more central loci earlier).
  - **Quest Max Trials Per Locus** — fewer Quest updates per locus (default 10; lower = shorter exam).
- **Heatmap / reliability**: unchanged — `FinishTest()` still calls `HeatmapGenerator.Generate` and `ReliabilityReport.Generate`.

---

## 5. Local testing in Unity Editor (before Android)

1. Open **PatientExam** (or run from **MainMenu** after build order is set).
2. Press **Play**. `SharedDoctorMirror` listens on **all interfaces** at port **8765**.
3. On the laptop:  
   `cd ClinicalViewer`  
   `npm install ws` (once)  
   **Windows:** `set UNITY_HOST=127.0.0.1`  
   **macOS/Linux:** `export UNITY_HOST=127.0.0.1`  
   `node unified-server.js`
4. Open **http://localhost:3000** — you should see **Connected**, JPEG mirror updates (if `MonitorFeed` is wired), trial rows, and the **Exam control** panel.
5. Use **Exam control → Start test on patient** or **Full battery**; watch **← ACK** lines in the command log. Patient app should load **PatientExam** and start sessions as if the phone received the command.

Firewall: allow Node and Unity Editor if prompted.

---

## 6. Running on Android (patient phone)

1. Build & run the URP scene on device; grant network permissions as required.
2. Phone and laptop on the **same Wi‑Fi**.
3. Note the phone’s **LAN IP** (Wi‑Fi settings).
4. `SharedDoctorMirror` listens on **0.0.0.0:8765** — reachable from LAN.
5. Session data writes under `Application.persistentDataPath` (see `INTEGRATION.md` for paths).

---

## 7. Viewing the mirror on the laptop (stream layer)

1. Install Node dependency once:  
   `cd ClinicalViewer && npm install ws`
2. Point the bridge at the phone (or **127.0.0.1** for Editor):  
   `set UNITY_HOST=192.168.x.x` (Windows) or `export UNITY_HOST=...` (macOS/Linux)
3. Run:  
   `node unified-server.js`
4. Open **http://localhost:3000** (or `http://<laptop-ip>:3000` from another machine on LAN).

**Flow:** Unity broadcasts JSON (`TRIAL`, `TEST_COMPLETE`, `SESSION_STATE`, `STIMULUS_STATE`, `TRIAL_OVERLAY`, `PATIENT_COMMAND_ACK`, …) and binary JPEG frames → **unified-server.js** → SSE → **unified-dashboard.html**.

**Doctor → patient:** dashboard **Exam control** uses **`POST /api/command`** → Node forwards **`DOCTOR_COMMAND`** JSON to Unity → **`DoctorInboundProcessor`** → **`AppFlowCoordinator.HandleDoctorCommand`**.

**Doctor overlay (Part 4):** bottom strip on the phone mirror shows **test name, stimulus, intensity, coords/index, progress, hit/miss label, reliability** (reliability fills after each test’s `TEST_COMPLETE` for that `testId`).

---

## 8. How tests connect (data flow)

```
MainMenuUI / Doctor dashboard (POST /api/command)
        │
        ▼
 AppFlowCoordinator → LoadPatientExam / StartSingleTest / …
        │
        ▼
SessionStartUI / UnifiedTestManager (optional)
        │
        ▼
 AppOrchestrator.StartSession(patient, eye, age [, optional testId filter])
        │
        ├─► VisualTestManager.LoadTest(id)   (if activateTestsViaManager)
        │
        ├─► ITestModule.StartTest(SessionContext)
        │
        ├─► OnTrialEnd(TestTrialEvent)  ──► CSV + SharedDoctorMirror (TRIAL + TRIAL_OVERLAY)
        │
        └─► OnTestComplete(TestResult) ──► files + SharedDoctorMirror (TEST_COMPLETE)
```

- **UnifiedTestManager**  
  - `StartFullSession(...)` — all modules in **VisualTestManager** list order.  
  - `StartFullSession(..., UnifiedTestManager.DefaultFullBatteryOrder)` — force a canonical order (skips missing ids).  
  - `StartSingleTestSession(..., TestIds.Csv1000)` — one test only.  
  - `SwitchActiveModule(testId)` — activate a root **without** starting a session.

---

## 9. Test IDs (code & dashboard)

Use these exact strings in Unity and JavaScript:

`PERIMETRY_24_2`, `CSV_1000`, `PELLI_ROBSON`, `SPARCS`, `MOTION_DETECTION`, `EDGE_DETECTION`, `PATTERN_DETECTION`

---

## 10. Extending

1. Implement `IVisualTestModule` (or `ITestModule`).
2. Add a root entry on **VisualTestManager**.
3. Add a row to **`OphthalTestCatalog`** (asset) with matching **`testId`** and `enabled` ticked.
4. Optionally add `RENDER_MODULES['YOUR_ID']` in `unified-dashboard.html` for a custom live panel; add the same id to **`DOCTOR_TEST_IDS`** in the dashboard script if you want a quick **Exam control** button.
5. Optionally extend `AppOrchestrator.BroadcastTrialOverlay` parsers for new `extraJson` shapes.

---

## 11. Quick checklist before first patient run

- [ ] **Build Settings:** `MainMenu` first, `PatientExam` included; scene names match **AppFlowCoordinator**.
- [ ] **MainMenu:** `AppFlowCoordinator`, `DoctorInboundProcessor`, `SharedDoctorMirror`, `MainMenuUI` (+ **EventSystem**).
- [ ] **PatientExam:** `AppOrchestrator` ↔ `VisualTestManager` wired; test list complete; **MonitorFeed** on mirror.
- [ ] Perimetry: `DoctorMirror` reference cleared on `PerimetryMaster`.
- [ ] `unified-server.js` **`UNITY_HOST`** = phone IP (or **127.0.0.1** in Editor).
- [ ] Browser dashboard shows **Connected**, mirror frames updating, **Exam control** ACKs when you start a test.

---

## 12. Verify mirroring and data return

1. With Play mode (Editor) or device build, confirm **SSE** receives `SESSION_STATE` after `StartSession`.
2. Run a short test; confirm **Trial feed** rows and **`TRIAL_OVERLAY`** on the mirror strip update.
3. Confirm **`ClinicalViewer/logs/session_*.jsonl`** grows with lines whose `receivedAt` is ISO‑8601 and payload includes `messageType` / trial fields.
4. From **Exam control**, send **Return menu** and confirm patient scene returns to **MainMenu** and ACK shows **ok**.

---

*End of unified integration (menu + doctor control + mirror + logging).*
