using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    [Serializable]
    private class Configuration
    {
        public string platform = "";
        public bool development = false;
        public bool androidAab = false;
        public bool androidSign = false;
        public string androidPackage = "";
        public int androidVersionCode = 1;
        public string iosBundleId = "";
        public string iosBuildNumber = "1";
    }

    public static void BuildCI()
    {
        var configPath = Argument("-ciConfig");
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            throw new Exception("CI configuration is missing. Run the workflow preparation step first.");
        var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(configPath));
        switch (config.platform)
        {
            case "Windows": BuildWindows(config); break;
            case "Android": BuildAndroid(config); break;
            case "iOS": BuildiOS(config); break;
            default: throw new Exception("Unsupported CI platform: " + config.platform);
        }
    }

    private static string Argument(string name, string fallback = "")
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i].Substring(name.Length + 1);
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return fallback;
    }

    private static string RequiredArgument(string name)
    {
        var value = Argument(name);
        if (string.IsNullOrEmpty(value)) throw new Exception("Missing signing parameter: " + name);
        return value;
    }

    private static void BuildWindows(Configuration config)
    {
        // Linux can cross-build Mono Windows players. IL2CPP needs a Windows toolchain.
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
            throw new Exception("This Windows workflow requires the Mono scripting backend. Windows IL2CPP needs a Windows runner/toolchain.");
        Build(BuildTarget.StandaloneWindows64, "build/Windows/Game.exe", config.development);
    }

    private static void BuildAndroid(Configuration config)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, config.androidPackage);
        PlayerSettings.Android.bundleVersionCode = config.androidVersionCode;
        PlayerSettings.Android.useCustomKeystore = config.androidSign;
        if (config.androidSign)
        {
            PlayerSettings.Android.keystoreName = Path.GetFullPath(RequiredArgument("-androidKeystoreName"));
            PlayerSettings.Android.keystorePass = RequiredArgument("-androidKeystorePass");
            PlayerSettings.Android.keyaliasName = RequiredArgument("-androidKeyaliasName");
            PlayerSettings.Android.keyaliasPass = RequiredArgument("-androidKeyaliasPass");
        }
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.buildAppBundle = config.androidAab;
        Build(BuildTarget.Android, "build/Android/Game" + (config.androidAab ? ".aab" : ".apk"), config.development);
    }

    private static void BuildiOS(Configuration config)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, config.iosBundleId);
        PlayerSettings.iOS.buildNumber = config.iosBuildNumber;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        Build(BuildTarget.iOS, "build/iOS", config.development);
    }

    private static void Build(BuildTarget target, string output, bool development)
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0) throw new Exception("No enabled scenes. Add a scene to the project's Build Settings before building.");
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "build");
        var options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None;
        var report = BuildPipeline.BuildPlayer(scenes, output, target, options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"{target} build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
        Debug.Log($"Built {target}: {output}; development={development}");
    }
}
