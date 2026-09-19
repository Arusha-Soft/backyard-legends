using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    /// <summary>
    /// Firebase init shared by Auth/Firestore.
    /// ParrelSync: uses a named FirebaseApp + disables Firestore persistence in Editor so
    /// two editors do not crash on the shared LevelDB LOCK (FIRESTORE INTERNAL ASSERTION).
    /// </summary>
    public static class FirebaseBootstrap
    {
        private static Task<bool> dependencyTask;
        private static bool initialized;
        private static bool available;
        private static global::Firebase.FirebaseApp app;
        private static global::Firebase.Auth.FirebaseAuth auth;
        private static global::Firebase.Firestore.FirebaseFirestore firestore;
        private static bool firestoreConfigured;

        public static bool IsAvailable => available;
        public static bool IsInitialized => initialized;
        public static global::Firebase.FirebaseApp App => app;
        public static bool IsEditorClone => DetectEditorClone();

        public static Task<bool> EnsureInitializedAsync()
        {
            if (initialized)
            {
                return Task.FromResult(available);
            }

            if (dependencyTask != null)
            {
                return dependencyTask;
            }

            dependencyTask = InitializeInternalAsync();
            return dependencyTask;
        }

        public static global::Firebase.Auth.FirebaseAuth GetAuth()
        {
            if (!available || app == null)
            {
                return null;
            }

            if (auth == null)
            {
                auth = global::Firebase.Auth.FirebaseAuth.GetAuth(app);
            }

            return auth;
        }

        public static global::Firebase.Firestore.FirebaseFirestore GetFirestore()
        {
            if (!available || app == null)
            {
                return null;
            }

            if (firestore == null)
            {
                firestore = global::Firebase.Firestore.FirebaseFirestore.GetInstance(app);
            }

            // Must be set before other Firestore calls; required for multi-editor (ParrelSync).
            if (!firestoreConfigured)
            {
#if UNITY_EDITOR
                firestore.Settings.PersistenceEnabled = false;
                Debug.Log($"Firestore persistence disabled for Editor (clone={IsEditorClone}, app={app.Name}).");
#endif
                firestoreConfigured = true;
            }

            return firestore;
        }

        private static async Task<bool> InitializeInternalAsync()
        {
            try
            {
                var status = await global::Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != global::Firebase.DependencyStatus.Available)
                {
                    Debug.LogWarning($"Firebase dependencies not available: {status}. Local play continues without auth.");
                    available = false;
                    initialized = true;
                    return false;
                }

                var defaultApp = global::Firebase.FirebaseApp.DefaultInstance;
                var appName = ResolveAppName();
                if (string.Equals(appName, global::Firebase.FirebaseApp.DefaultName, StringComparison.Ordinal))
                {
                    app = defaultApp;
                }
                else
                {
                    app = global::Firebase.FirebaseApp.GetInstance(appName)
                          ?? global::Firebase.FirebaseApp.Create(defaultApp.Options, appName);
                    Debug.Log($"Firebase using named app '{appName}' for ParrelSync/editor clone.");
                }

                // Configure Firestore immediately so Play Mode does not open LevelDB.
                _ = GetFirestore();

                available = true;
                initialized = true;
                Debug.Log($"Firebase initialized (app={app.Name}).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Firebase init failed (missing config or native libs). Local play continues without auth. {ex.Message}");
                available = false;
                initialized = true;
                return false;
            }
        }

        private static string ResolveAppName()
        {
#if UNITY_EDITOR
            if (DetectEditorClone())
            {
                var folder = "clone";
                try
                {
                    folder = new DirectoryInfo(Application.dataPath).Parent?.Name ?? "clone";
                }
                catch
                {
                    folder = "clone";
                }

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    folder = folder.Replace(c, '_');
                }

                return global::Firebase.FirebaseApp.DefaultName + "_" + folder;
            }
#endif
            return global::Firebase.FirebaseApp.DefaultName;
        }

        private static bool DetectEditorClone()
        {
#if UNITY_EDITOR
            var dataPath = Application.dataPath ?? string.Empty;
            if (dataPath.IndexOf("_clone_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-cloneArg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "--cloneArg", StringComparison.OrdinalIgnoreCase) ||
                    (args[i] != null && args[i].IndexOf("clone", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     args[i].IndexOf("ParrelSync", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
#endif
            return false;
        }
    }
}
