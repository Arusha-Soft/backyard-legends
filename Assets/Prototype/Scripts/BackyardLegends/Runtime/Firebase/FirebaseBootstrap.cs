using System;
using System.Threading.Tasks;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    public static class FirebaseBootstrap
    {
        private static Task<bool> dependencyTask;
        private static bool initialized;
        private static bool available;

        public static bool IsAvailable => available;
        public static bool IsInitialized => initialized;

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

                _ = global::Firebase.FirebaseApp.DefaultInstance;
                available = true;
                initialized = true;
                Debug.Log("Firebase initialized.");
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
    }
}
