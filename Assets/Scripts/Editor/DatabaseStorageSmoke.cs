#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using OphthalSuite.Core;
using OphthalSuite.Core.Database;
using UnityEditor;
using UnityEngine;

namespace OphthalSuite.EditorTools
{
    public static class DatabaseStorageSmoke
    {
        public static void Run()
        {
            string reportPath = Path.Combine(Application.dataPath, "..", "DatabaseStorageSmokeResult.txt");

            try
            {
                var go = new GameObject("DatabaseStorageSmoke_DatabaseManager");
                var db = go.AddComponent<DatabaseManager>();
                InitDatabaseForEditMode(db);

                string sessionId = "SMOKE_STORAGE_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var ctx = new SessionContext
                {
                    sessionId = sessionId,
                    patientId = "P_SMOKE",
                    eye = "OD",
                    age = 0,
                    deviceId = "batchmode-smoke"
                };

                db.InsertSession(ctx);
                db.InsertTrial(new TestTrialEvent
                {
                    sessionId = sessionId,
                    testId = "CSV_1000",
                    trialIndex = 1,
                    stimulusId = "letter_C",
                    isCatch = false,
                    hit = true,
                    reactionTimeSec = 0.42f,
                    fixationLost = false,
                    extraJson = "{\"target\":\"C\",\"chosen\":\"C\",\"script\":\"en\",\"logMichelson\":-0.45}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                db.InsertTestResult(new TestResult
                {
                    sessionId = sessionId,
                    testId = "CSV_1000",
                    displayName = "CSV Letters",
                    durationSeconds = 3.2f,
                    falsePosRate = 0f,
                    falseNegRate = 0f,
                    fixationLossRate = 0f,
                    reliabilityCategory = "Acceptable",
                    fullResultJson = "{\"totalTrials\":1,\"accuracy\":1.0}",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
                db.UpdateSessionEnd(sessionId, 3.2f, "completed");

                string sessionFile = Path.Combine(Application.persistentDataPath, "db", "sessions", sessionId + ".json");
                string sessionFileJson = File.Exists(sessionFile) ? File.ReadAllText(sessionFile) : "";
                string exported = db.ExportSessionJson(sessionId);

                bool ok =
                    File.Exists(sessionFile) &&
                    exported.Contains("\"extraJson\"") &&
                    exported.Contains("\"fullResult\"") &&
                    exported.Contains("\"patientId\":\"P_SMOKE\"") &&
                    !exported.Contains("\"age\"") &&
                    !sessionFileJson.Contains("\"age\"");

                File.WriteAllText(reportPath,
                    "ok=" + ok + Environment.NewLine +
                    "sessionId=" + sessionId + Environment.NewLine +
                    "sessionFile=" + sessionFile + Environment.NewLine +
                    "exportContainsExtraJson=" + exported.Contains("\"extraJson\"") + Environment.NewLine +
                    "exportContainsFullResult=" + exported.Contains("\"fullResult\"") + Environment.NewLine +
                    "exportContainsAge=" + exported.Contains("\"age\"") + Environment.NewLine +
                    "sessionFileContainsAge=" + sessionFileJson.Contains("\"age\"") + Environment.NewLine +
                    "export=" + exported + Environment.NewLine);

                UnityEngine.Object.DestroyImmediate(go);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception ex)
            {
                File.WriteAllText(reportPath, "ok=False" + Environment.NewLine + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void InitDatabaseForEditMode(DatabaseManager db)
        {
            string dbRoot = Path.Combine(Application.persistentDataPath, "db");
            string sessionsDir = Path.Combine(dbRoot, "sessions");
            string indexPath = Path.Combine(dbRoot, "sessions_index.json");
            Directory.CreateDirectory(sessionsDir);

            Type dbType = typeof(DatabaseManager);
            Type indexType = dbType.GetNestedType("SessionIndex", BindingFlags.NonPublic);
            object index = Activator.CreateInstance(indexType);

            dbType.GetField("_dbRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, dbRoot);
            dbType.GetField("_sessionsDir", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, sessionsDir);
            dbType.GetField("_indexPath", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, indexPath);
            dbType.GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, index);
        }
    }
}
#endif
