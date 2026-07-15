using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TexChatbot.Editor
{
    // Batch-mode Android build for the RAG app. The large models (GGUF 2.9G + fp16 ONNX
    // 1.1G) together exceed the 4GB ZIP limit, so they CANNOT ship in a single APK or OBB;
    // they are pushed to persistentDataPath separately (push_assets.sh). StreamingAssets
    // therefore holds only the small files (~12MB) -> a lean APK, Split Binary off by
    // default. Pass -split only if all StreamingAssets happen to fit one OBB (<4GB).
    //
    // Invoked by build_unity_android.sh:
    //   Unity -batchmode -nographics -projectPath <proj> -buildTarget Android \
    //         -executeMethod TexChatbot.Editor.BuildAndroid.Build \
    //         -buildOutput <dir> [-versionCode N] [-split] [-development] -logFile -
    public static class BuildAndroid
    {
        public static void Build()
        {
            string outputDir = GetArg("-buildOutput")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "Build", "Android");
            string versionCodeArg = GetArg("-versionCode");
            bool development = HasFlag("-development");
            bool splitBinary = HasFlag("-split");

            Directory.CreateDirectory(outputDir);

            // Ensure the active target is Android before touching its player settings.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            // IL2CPP + arm64 (Galaxy S25), matching E's native plugins.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;

            // Off by default: the big models are delivered out-of-band (push_assets.sh), so
            // StreamingAssets is small and a single APK suffices. -split opts into an OBB.
            PlayerSettings.Android.useAPKExpansionFiles = splitBinary;

            if (int.TryParse(versionCodeArg, out int vc))
                PlayerSettings.Android.bundleVersionCode = vc;

            string apkPath = Path.Combine(outputDir, Sanitize(PlayerSettings.productName) + ".apk");

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development
                    ? (BuildOptions.Development | BuildOptions.AllowDebugging)
                    : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildAndroid] FAILED: result={summary.result} errors={summary.totalErrors}");
                EditorApplication.Exit(1);
            }

            Debug.Log($"[BuildAndroid] OK apk={apkPath} size={summary.totalSize}B " +
                      $"versionCode={PlayerSettings.Android.bundleVersionCode} " +
                      $"splitBinary={PlayerSettings.Android.useAPKExpansionFiles}");
            EditorApplication.Exit(0);
        }

        private static string[] EnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException(
                    "No enabled scenes in Build Settings. Add the app scene before building.");
            return scenes;
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static bool HasFlag(string name) => Environment.GetCommandLineArgs().Contains(name);

        private static string Sanitize(string s) =>
            string.Concat((s ?? "App").Split(Path.GetInvalidFileNameChars()));
    }
}
