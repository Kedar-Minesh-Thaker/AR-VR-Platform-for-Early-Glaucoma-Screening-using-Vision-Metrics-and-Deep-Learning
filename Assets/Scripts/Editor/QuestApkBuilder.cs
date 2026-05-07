#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OphthalSuite.EditorTools
{
    public static class QuestApkBuilder
    {
        [MenuItem("OphthalSuite/Build Quest APK")]
        public static void BuildQuestApk()
        {
            string buildDir = Path.Combine(Application.dataPath, "..", "Builds");
            Directory.CreateDirectory(buildDir);

            string apkPath = Path.GetFullPath(Path.Combine(buildDir, "Perimetry242-Quest.apk"));
            string[] scenes = { "Assets/Scenes/SampleScene.unity" };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Quest APK build succeeded: " + apkPath);
                EditorUtility.RevealInFinder(apkPath);
            }
            else
            {
                Debug.LogError("Quest APK build failed: " + report.summary.result);
            }
        }
    }
}
#endif
