#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OphthalSuite.Core;
using OphthalSuite.Core.Database;
using UnityEngine;

namespace OphthalSuite.Tests
{
    public class DatabaseStorageTests
    {
        private GameObject _go;
        private DatabaseManager _db;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DatabaseManager_Test");
            _db = _go.AddComponent<DatabaseManager>();
            InitDatabaseForEditMode(_db);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Database_StoresSessionTrialAndResult_WithPerTestPayloads()
        {
            var ctx = new SessionContext
            {
                sessionId = "TEST_SESSION_STORAGE",
                patientId = "P_TEST",
                eye = "OD",
                age = 0,
                deviceId = "editor-test"
            };

            _db.InsertSession(ctx);
            _db.InsertTrial(new TestTrialEvent
            {
                sessionId = ctx.sessionId,
                testId = "CSV_1000",
                trialIndex = 1,
                stimulusId = "CSV_letter_C",
                isCatch = false,
                hit = true,
                reactionTimeSec = 0.42f,
                fixationLost = false,
                extraJson = "{\"target\":\"C\",\"chosen\":\"C\",\"script\":\"en\",\"logMichelson\":-0.45}",
                timestamp = "2026-05-05T00:00:00Z"
            });
            _db.InsertTestResult(new TestResult
            {
                sessionId = ctx.sessionId,
                testId = "CSV_1000",
                displayName = "CSV Letters",
                durationSeconds = 3.2f,
                falsePosRate = 0f,
                falseNegRate = 0f,
                fixationLossRate = 0f,
                reliabilityCategory = "Acceptable",
                fullResultJson = "{\"totalTrials\":1,\"accuracy\":1.0}",
                timestamp = "2026-05-05T00:00:03Z"
            });
            _db.UpdateSessionEnd(ctx.sessionId, 3.2f, "completed");

            var sessions = _db.GetSessionsByPatient("P_TEST");
            Assert.AreEqual(1, sessions.Count);
            Assert.AreEqual("P_TEST", sessions[0].patientId);
            Assert.AreEqual("OD", sessions[0].eye);

            var trials = _db.GetTrialsBySession(ctx.sessionId);
            Assert.AreEqual(1, trials.Count);
            Assert.AreEqual("CSV_1000", trials[0].testId);
            Assert.IsTrue(trials[0].extraJson.Contains("\"target\":\"C\""));

            var results = _db.GetResultsBySession(ctx.sessionId);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Acceptable", results[0].reliability);
            Assert.IsTrue(results[0].fullResult.Contains("\"accuracy\":1.0"));

            string exported = _db.ExportSessionJson(ctx.sessionId);
            Assert.IsTrue(exported.Contains("\"extraJson\""));
            Assert.IsTrue(exported.Contains("\"fullResult\""));
            string sessionFile = Path.Combine(Application.persistentDataPath, "db", "sessions", ctx.sessionId + ".json");
            Assert.IsTrue(File.Exists(sessionFile));
            Assert.IsFalse(File.ReadAllText(sessionFile).Contains("\"age\""));
        }

        private static void InitDatabaseForEditMode(DatabaseManager db)
        {
            string dbRoot = Path.Combine(Application.persistentDataPath, "db");
            string sessionsDir = Path.Combine(dbRoot, "sessions");
            string indexPath = Path.Combine(dbRoot, "sessions_index.json");
            Directory.CreateDirectory(sessionsDir);

            var dbType = typeof(DatabaseManager);
            var indexType = dbType.GetNestedType("SessionIndex", BindingFlags.NonPublic);
            object index = System.Activator.CreateInstance(indexType);

            dbType.GetField("_dbRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, dbRoot);
            dbType.GetField("_sessionsDir", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, sessionsDir);
            dbType.GetField("_indexPath", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, indexPath);
            dbType.GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(db, index);
        }
    }
}
#endif
