#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MetroSim
{
    public static class BuildScript
    {
        [MenuItem("MetroSim/Build Windows 64")]
        public static void BuildWindows64()
        {
            string outputPath = "Builds/Windows/MetroSim.exe";

            var options = new BuildPlayerOptions
            {
                scenes        = new[] { "Assets/save.unity" },
                locationPathName = outputPath,
                target        = BuildTarget.StandaloneWindows64,
                options       = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[Build] Success! Output: {outputPath}  Size: {report.summary.totalSize / 1024 / 1024} MB");
            else
                Debug.LogError($"[Build] Failed: {report.summary.result}");
        }

        // Called by Unity -executeMethod in batch mode
        public static void BatchBuildWindows64()
        {
            BuildWindows64();
        }
    }
}
#endif
