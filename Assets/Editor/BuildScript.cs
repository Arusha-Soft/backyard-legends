using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    public static void BuildWindows()
    {
        // Read the -development custom parameter (presence is enough)
        var args = Environment.GetCommandLineArgs();
        bool development = Array.Exists(args, a => a == "-development");

        // Use only enabled scenes
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        // Ensure output directory exists
        var buildPath = Path.Combine("build", "Windows");
        Directory.CreateDirectory(buildPath);

        // IMPORTANT: include .exe in the filename
        var exeName = PlayerSettings.productName + ".exe";
        var locationPathName = Path.Combine(buildPath, exeName);

        // Build options
        var options = BuildOptions.None;
        if (development)
        {
            options |= BuildOptions.Development | BuildOptions.AllowDebugging;
        }

        // Build and validate
        var report = BuildPipeline.BuildPlayer(scenes, locationPathName, BuildTarget.StandaloneWindows64, options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Windows build failed: {report.summary.result}");
        }

        Debug.Log($"Built Windows player: {locationPathName}");
    }

    public static void BuildAndroid()
    {
        var args = Environment.GetCommandLineArgs();

        // Helpers to read flags like: "-development true" / "-aab false"
        bool GetBoolArg(string name, bool defaultValue = false)
        {
            // support "-name value" and "-name=value"
            var idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                                 a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return defaultValue;

            // Case: "-name=value"
            var eq = args[idx].IndexOf('=');
            if (eq >= 0)
            {
                var v = args[idx].Substring(eq + 1);
                return IsTrue(v, defaultValue);
            }

            // Case: "-name value"
            if (idx + 1 < args.Length) return IsTrue(args[idx + 1], defaultValue);

            // If flag present but no value, treat as true (common CLI behavior)
            return true;
        }

        bool IsTrue(string v, bool fallback)
        {
            if (string.IsNullOrEmpty(v)) return fallback;
            v = v.Trim().ToLowerInvariant();
            return v is "1" or "true" or "yes" or "y" or "on";
        }

        // Parse inputs
        var development = GetBoolArg("-development", false);
        // Optional manual override, e.g. pass "-aab true" to force .aab even on dev,
        // or "-aab false" to force .apk on release.
        var aabOverride = (bool?)null;
        if (Array.Exists(args, a => a.StartsWith("-aab", StringComparison.OrdinalIgnoreCase)))
            aabOverride = GetBoolArg("-aab", true);

        var makeAab = aabOverride ?? !development; // default: dev=APK, release=AAB

        // Build options
        var options = BuildOptions.None;
        if (development)
        {
            options |= BuildOptions.Development;
            options |= BuildOptions.AllowDebugging;
        }

        // Ensure Android/Gradle & correct export mode
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false; // direct APK/AAB artifact
        EditorUserBuildSettings.buildAppBundle = makeAab;             // true => .aab, false => .apk

        // Paths
        var buildDir = "build/Android";
        Directory.CreateDirectory(buildDir);
        var ext = makeAab ? ".aab" : ".apk";
        var outPath = Path.Combine(buildDir, PlayerSettings.productName + ext);

        // Scenes to include (enabled only)
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = BuildTarget.Android,
            locationPathName = outPath,
            options = options
        });

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Android build failed: " + report.summary.result);
    }

    public static void BuildiOS()
    {
        var args = Environment.GetCommandLineArgs();
        var development = Array.Exists(args, arg => arg == "-development");

        var scenes = EditorBuildSettings.scenes;
        var buildPath = "build/iOS";

        var options = BuildOptions.None;
        if (development)
        {
            options |= BuildOptions.Development;
        }

        Directory.CreateDirectory(buildPath);
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iOS, options);
    }
}