using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    public sealed class UserProfileRecord
    {
        public string Uid { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Rating { get; set; } = 1000;
        public string Platform { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; } = true;
    }

    public static class UserProfileService
    {
        private const string CollectionName = "users";
        private const int DefaultRating = 1000;

        public static async Task<UserProfileRecord> EnsureUserProfileAsync(
            string uid,
            string displayName,
            string platform,
            bool isAnonymous)
        {
            if (string.IsNullOrEmpty(uid) || !FirebaseBootstrap.IsAvailable)
            {
                return null;
            }

            var db = global::Firebase.Firestore.FirebaseFirestore.DefaultInstance;
            var doc = db.Collection(CollectionName).Document(uid);
            var snapshot = await doc.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var existing = FromSnapshot(snapshot);
                var updates = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(displayName)
                    && !string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal)
                    && (!isAnonymous || string.IsNullOrWhiteSpace(existing.DisplayName) || existing.DisplayName == "Guest"))
                {
                    updates["displayName"] = displayName;
                    existing.DisplayName = displayName;
                }

                if (!string.IsNullOrWhiteSpace(platform) && existing.Platform != platform)
                {
                    updates["platform"] = platform;
                    existing.Platform = platform;
                }

                if (existing.IsAnonymous != isAnonymous)
                {
                    updates["isAnonymous"] = isAnonymous;
                    existing.IsAnonymous = isAnonymous;
                }

                updates["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp;

                if (updates.Count > 0)
                {
                    await doc.UpdateAsync(updates);
                }

                return existing;
            }

            var created = new UserProfileRecord
            {
                Uid = uid,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? (isAnonymous ? "Guest" : "Player") : displayName,
                Rating = DefaultRating,
                Platform = platform ?? string.Empty,
                IsAnonymous = isAnonymous
            };

            await doc.SetAsync(new Dictionary<string, object>
            {
                ["uid"] = created.Uid,
                ["displayName"] = created.DisplayName,
                ["rating"] = created.Rating,
                ["platform"] = created.Platform,
                ["isAnonymous"] = created.IsAnonymous,
                ["createdAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });

            Debug.Log($"Created Firestore user profile {uid}");
            return created;
        }

        private static UserProfileRecord FromSnapshot(global::Firebase.Firestore.DocumentSnapshot snapshot)
        {
            var data = snapshot.ToDictionary();
            return new UserProfileRecord
            {
                Uid = snapshot.Id,
                DisplayName = GetString(data, "displayName", "Guest"),
                Rating = GetInt(data, "rating", DefaultRating),
                Platform = GetString(data, "platform", string.Empty),
                IsAnonymous = GetBool(data, "isAnonymous", true)
            };
        }

        private static string GetString(IDictionary<string, object> data, string key, string fallback)
        {
            if (data != null && data.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }

            return fallback;
        }

        private static int GetInt(IDictionary<string, object> data, string key, int fallback)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool GetBool(IDictionary<string, object> data, string key, bool fallback)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
