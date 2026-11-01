using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Build menu: Windows test build, Android test APK, and (later) the Play Store bundle.
    /// Each build writes a one-line summary to Builds/last_build.txt.
    /// </summary>
    public static class BuildTools
    {
        [MenuItem("Neon Brawl/Build/Windows (test)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/NeonBrawl.exe");

        /// <summary>PC test build into Builds/PC (command line: -executeMethod NeonBrawl.EditorTools.BuildTools.BuildPc).</summary>
        [MenuItem("Neon Brawl/Build/PC test (Builds/PC)")]
        public static void BuildPc()
        {
            var original = EditorUserBuildSettings.activeBuildTarget;
            bool ok = Log(Build(BuildTarget.StandaloneWindows64, "Builds/PC/NeonBrawl.exe"));
            if (EditorUserBuildSettings.activeBuildTarget != original)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Neon Brawl/Build/Android APK (test)")]
        public static void BuildAndroidApk()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/NeonBrawl.apk");
        }

        /// <summary>
        /// Both test builds in a row (active platform first, so only one platform switch). Meant for the
        /// command line, where it needs far less memory than the open editor:
        /// Unity.exe -batchmode -quit -projectPath . -executeMethod NeonBrawl.EditorTools.BuildTools.BuildAllTest -logFile Builds/build.log
        /// Every result line is appended to Builds/build_history.txt.
        /// </summary>
        [MenuItem("Neon Brawl/Build/Windows + Android (test)")]
        public static void BuildAllTest()
        {
            var original = EditorUserBuildSettings.activeBuildTarget;
            bool androidFirst = original == BuildTarget.Android;
            bool ok = true;
            for (int i = 0; i < 2; i++)
            {
                bool android = (i == 0) == androidFirst;
                EditorUserBuildSettings.buildAppBundle = false;
                var report = android
                    ? Build(BuildTarget.Android, "Builds/Android/NeonBrawl.apk")
                    : Build(BuildTarget.StandaloneWindows64, "Builds/Windows/NeonBrawl.exe");
                File.AppendAllText("Builds/build_history.txt", File.ReadAllText("Builds/last_build.txt") + System.Environment.NewLine);
                ok &= report.summary.result == BuildResult.Succeeded;
            }
            // leave the project on the platform it was on, so the editor reopens without a reimport
            if (EditorUserBuildSettings.activeBuildTarget != original)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public const string Version = "1.0.1";
        public const int VersionCode = 2;
        private const string KeystoreProperties = "Keystore/keystore.properties";

        /// <summary>
        /// Everything for a release, in one go (command line: -executeMethod NeonBrawl.EditorTools.BuildTools.BuildFinal):
        ///  1. Builds/Android/NeonBrawl-test-ads.apk – for playing on your own phone (Google test ads, safe to tap)
        ///  2. Builds/Android/NeonBrawl.apk          – release build with the real AdMob ads
        ///  3. Builds/Android/NeonBrawl.aab          – Play Store upload, only when an upload key is set up
        ///     (Keystore/keystore.properties with storeFile, storePassword, keyAlias, keyPassword; never commit it)
        ///  4. Builds/PC/NeonBrawl.exe               – PC test build
        /// </summary>
        [MenuItem("Neon Brawl/Build/Final (Android test + release + AAB, Windows)")]
        public static void BuildFinal() => BuildFinalCore(true);

        /// <summary>Same as <see cref="BuildFinal"/> without the PC build.</summary>
        [MenuItem("Neon Brawl/Build/Final Android only")]
        public static void BuildFinalAndroid() => BuildFinalCore(false);

        private static void BuildFinalCore(bool pc)
        {
            var original = EditorUserBuildSettings.activeBuildTarget;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            AppIconBaker.Apply();
            bool ok = true;

            EditorUserBuildSettings.buildAppBundle = false;
            ok &= Log(Build(BuildTarget.Android, "Builds/Android/NeonBrawl-test-ads.apk", "NB_TEST_ADS"));
            ok &= Log(Build(BuildTarget.Android, "Builds/Android/NeonBrawl.apk"));
            if (UseUploadKey())
            {
                EditorUserBuildSettings.buildAppBundle = true;
                ok &= Log(Build(BuildTarget.Android, "Builds/Android/NeonBrawl.aab"));
                EditorUserBuildSettings.buildAppBundle = false;
                PlayerSettings.Android.useCustomKeystore = false; // passwords stay out of the project files
            }
            else
            {
                File.AppendAllText("Builds/build_history.txt", $"{System.DateTime.Now:u} AAB skipped: no {KeystoreProperties} (upload key)" + System.Environment.NewLine);
            }
            if (pc) ok &= Log(Build(BuildTarget.StandaloneWindows64, "Builds/PC/NeonBrawl.exe"));

            if (EditorUserBuildSettings.activeBuildTarget != original)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Log(BuildReport report)
        {
            File.AppendAllText("Builds/build_history.txt", File.ReadAllText("Builds/last_build.txt") + System.Environment.NewLine);
            return report.summary.result == BuildResult.Succeeded;
        }

        /// <summary>Reads the upload key settings from a local, git-ignored file. False when there is none.</summary>
        private static bool UseUploadKey()
        {
            if (!File.Exists(KeystoreProperties)) return false;
            var map = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(KeystoreProperties))
            {
                int eq = raw.IndexOf('=');
                if (eq > 0 && !raw.TrimStart().StartsWith("#")) map[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
            }
            if (!map.TryGetValue("storeFile", out var file) || !File.Exists(file)) return false;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(file);
            PlayerSettings.Android.keystorePass = map.TryGetValue("storePassword", out var sp) ? sp : "";
            PlayerSettings.Android.keyaliasName = map.TryGetValue("keyAlias", out var ka) ? ka : "";
            PlayerSettings.Android.keyaliasPass = map.TryGetValue("keyPassword", out var kp) ? kp : PlayerSettings.Android.keystorePass;
            return true;
        }

        public static BuildReport Build(BuildTarget target, string path, params string[] defines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
                extraScriptingDefines = defines,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string line = $"{System.DateTime.Now:u} {target} {s.result} size={ShippedSize(path) / (1024f * 1024f):0.0}MB time={s.totalTime.TotalSeconds:0}s errors={s.totalErrors} -> {path}";
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/last_build.txt", line);
            if (s.result == BuildResult.Succeeded) Debug.Log("[NeonBrawl] " + line);
            else Debug.LogError("[NeonBrawl] " + line);
            return report;
        }

        /// <summary>What the player downloads: the APK file, or the Windows folder minus debug-only folders.</summary>
        private static long ShippedSize(string path)
        {
            if (path.EndsWith(".apk") || path.EndsWith(".aab")) return File.Exists(path) ? new FileInfo(path).Length : 0;
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) return 0;
            long total = 0;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                if (!f.Contains("DoNotShip") && !f.Contains("DontShip")) total += new FileInfo(f).Length;
            return total;
        }
    }
}
