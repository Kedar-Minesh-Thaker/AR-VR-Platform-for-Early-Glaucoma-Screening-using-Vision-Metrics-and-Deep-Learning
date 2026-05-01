// VisualTestManager.cs
// Registers visual test roots, exposes ordered ITestModule list, and load/unload for scene activation.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OphthalSuite.Core
{
    [DefaultExecutionOrder(-50)]
    public class VisualTestManager : MonoBehaviour
    {
        [Serializable]
        public class RegisteredTest
        {
            [Tooltip("Root GameObject for this test (must host or child ITestModule — resolved on Awake).")]
            public GameObject root;

            [Tooltip("If true, root starts active. Otherwise inactive until LoadTest.")]
            public bool startActive;
        }

        [SerializeField] private List<RegisteredTest> tests = new List<RegisteredTest>();

        private readonly List<ITestModule> _ordered = new List<ITestModule>();
        private readonly Dictionary<string, RegisteredTest> _byId = new Dictionary<string, RegisteredTest>(StringComparer.Ordinal);
        private ITestModule _loaded;
        private GameObject _loadedRoot;

        public ITestModule CurrentLoaded => _loaded;
        public IReadOnlyList<ITestModule> OrderedModules => _ordered;

        private void Awake()
        {
            _ordered.Clear();
            _byId.Clear();

            foreach (var reg in tests)
            {
                if (reg?.root == null) continue;

                var module = reg.root.GetComponent<ITestModule>();
                if (module == null)
                    module = reg.root.GetComponentInChildren<ITestModule>(true);

                if (module == null)
                {
                    Debug.LogError($"VisualTestManager: no ITestModule on '{reg.root.name}'.");
                    continue;
                }

                string id = module.TestId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"VisualTestManager: empty TestId on '{reg.root.name}'.");
                    continue;
                }

                if (_byId.ContainsKey(id))
                {
                    Debug.LogError($"VisualTestManager: duplicate TestId '{id}'.");
                    continue;
                }

                _byId[id] = reg;
                _ordered.Add(module);
                reg.root.SetActive(reg.startActive);
            }
        }

        /// <summary>Ordered modules for <see cref="AppOrchestrator"/> or custom runners.</summary>
        public IReadOnlyList<ITestModule> GetModulesInOrder() => _ordered;

        /// <summary>Activates the matching test root and deactivates others. Does not start the test.</summary>
        public bool LoadTest(string testId)
        {
            if (!_byId.TryGetValue(testId, out var reg))
            {
                Debug.LogWarning($"VisualTestManager: unknown TestId '{testId}'.");
                return false;
            }

            foreach (var kv in _byId.Values)
            {
                if (kv.root != null)
                    kv.root.SetActive(false);
            }

            reg.root.SetActive(true);
            _loadedRoot = reg.root;
            _loaded = reg.root.GetComponent<ITestModule>() ?? reg.root.GetComponentInChildren<ITestModule>(true);
            return true;
        }

        /// <summary>Stops the current test if running, then deactivates its root.</summary>
        public void UnloadCurrent()
        {
            if (_loaded != null && _loaded.IsRunning)
                _loaded.StopTest();

            if (_loadedRoot != null)
                _loadedRoot.SetActive(false);

            _loaded = null;
            _loadedRoot = null;
        }

        public bool TryGetModule(string testId, out ITestModule module)
        {
            module = null;
            if (!_byId.TryGetValue(testId, out var reg) || reg.root == null) return false;
            module = reg.root.GetComponent<ITestModule>() ?? reg.root.GetComponentInChildren<ITestModule>(true);
            return module != null;
        }
    }
}
