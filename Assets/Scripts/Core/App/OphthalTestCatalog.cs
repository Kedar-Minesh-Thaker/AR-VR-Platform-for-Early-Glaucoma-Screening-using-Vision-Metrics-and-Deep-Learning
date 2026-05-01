// OphthalTestCatalog.cs
// Data-driven menu: assign in Inspector — menu code never hardcodes test logic.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OphthalSuite.Core
{
    [CreateAssetMenu(menuName = "OphthalSuite/Test Catalog", fileName = "OphthalTestCatalog")]
    public class OphthalTestCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Must match ITestModule.TestId on the module.")]
            public string testId;
            public string displayName;
            [Tooltip("Optional subtitle on menu button.")]
            public string subtitle;
            public bool enabled = true;
        }

        public List<Entry> tests = new List<Entry>();
    }
}
