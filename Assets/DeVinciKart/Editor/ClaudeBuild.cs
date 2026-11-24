#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>Export Windows du jeu : menu « DeVinci Kart / Exporter le jeu (Windows) ».</summary>
    [InitializeOnLoad]
    static class ClaudeBuild
    {
        const string Request = "Temp/claude_build_request";
        const string OutDir = "Build";

        static ClaudeBuild() { EditorApplication.delayCall += () => { if (File.Exists(Request)) { File.Delete(Request); Build(); } }; }

        [MenuItem("DeVinci Kart/Exporter le jeu (Windows)")]
        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, "DeVinci Kart.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = $"result {s.result}\nerrors {s.totalErrors} warnings {s.totalWarnings}\nsize {s.totalSize / (1024 * 1024)} MB\ntime {s.totalTime}\noutput {Path.GetFullPath(options.locationPathName)}\n";
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) msg += "ERR " + m.content + "\n";
            File.WriteAllText("Temp/claude_build.txt", msg);
            Debug.Log("[DeVinci Kart] Export : " + msg);
            if (s.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(options.locationPathName);
        }
    }
}
#endif
