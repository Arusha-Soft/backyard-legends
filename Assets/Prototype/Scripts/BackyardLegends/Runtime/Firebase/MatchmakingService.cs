using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    /// <summary>
    /// Quick Match queue via Firestore. Cloud Functions (firebase/functions) perform the same
    /// match when deployed; this client also runs a transactional matcher so Editor testing works
    /// before Functions are deployed.
    /// </summary>
    public static class MatchmakingService
    {
        public const string QueueCollection = "matchQueue";
        public const string BucketsCollection = "matchBuckets";

        public sealed class QueueTicket
        {
            public string Uid = string.Empty;
            public string Mode = "Classic";
            public int TargetScore = 100;
            public string Region = "auto";
            public string Status = "queued"; // queued | matched | cancelled
            public string TableId = string.Empty;
            public string JoinCode = string.Empty;
            public bool IsHost;
            public double QueuedAtUnix;
        }

        public static async Task<QueueTicket> QueueForMatchAsync(
            string uid,
            string displayName,
            RuleSetDefinition rules,
            string region = "auto")
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new InvalidOperationException("Signed-in uid required for Quick Match.");
            }

            await FirebaseBootstrap.EnsureInitializedAsync();
            if (!FirebaseBootstrap.IsAvailable)
            {
                throw new InvalidOperationException("Firebase unavailable — cannot Quick Match.");
            }

            var db = FirebaseBootstrap.GetFirestore();
            var mode = rules?.DisplayName ?? "Classic";
            var target = rules?.TargetScore ?? 100;
            var bucketId = $"{mode}_{target}_{region}".Replace(" ", string.Empty);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ticket = new QueueTicket
            {
                Uid = uid,
                Mode = mode,
                TargetScore = target,
                Region = region ?? "auto",
                Status = "queued",
                QueuedAtUnix = now
            };

            await db.Collection(QueueCollection).Document(uid).SetAsync(new Dictionary<string, object>
            {
                ["uid"] = uid,
                ["displayName"] = displayName ?? "Player",
                ["mode"] = mode,
                ["targetScore"] = target,
                ["region"] = region ?? "auto",
                ["bucketId"] = bucketId,
                ["status"] = "queued",
                ["tableId"] = string.Empty,
                ["joinCode"] = string.Empty,
                ["isHost"] = false,
                ["queuedAt"] = now,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });

            await TryFormMatchForBucketAsync(bucketId, mode, target, rules);
            return ticket;
        }

        public static Task TryFormMatchForBucketAsync(
            string bucketId,
            string mode,
            int targetScore,
            RuleSetDefinition rules)
        {
            return TryFormMatchAsync(bucketId, mode, targetScore, rules);
        }

        public static async Task CancelQueueAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid) || !FirebaseBootstrap.IsAvailable)
            {
                return;
            }

            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(QueueCollection).Document(uid).UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = "cancelled",
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static ListenerRegistrationWrapper ListenTicket(
            string uid,
            Action<QueueTicket> onChanged,
            Action<Exception> onError = null)
        {
            if (!FirebaseBootstrap.IsAvailable || string.IsNullOrWhiteSpace(uid))
            {
                return null;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var registration = db.Collection(QueueCollection).Document(uid).Listen(snapshot =>
            {
                if (!snapshot.Exists)
                {
                    return;
                }

                try
                {
                    onChanged?.Invoke(FromSnapshot(snapshot));
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            });

            return new ListenerRegistrationWrapper(registration);
        }

        public static async Task<QueueTicket> GetTicketAsync(string uid)
        {
            if (!FirebaseBootstrap.IsAvailable || string.IsNullOrWhiteSpace(uid))
            {
                return null;
            }

            var snap = await FirebaseBootstrap.GetFirestore().Collection(QueueCollection).Document(uid).GetSnapshotAsync();
            return snap.Exists ? FromSnapshot(snap) : null;
        }

        private static async Task TryFormMatchAsync(
            string bucketId,
            string mode,
            int targetScore,
            RuleSetDefinition rules)
        {
            var db = FirebaseBootstrap.GetFirestore();
            var formed = false;
            string tableId = null;
            string hostUid = null;
            var matchedUids = new List<string>();
            var displayNames = new Dictionary<string, string>();

            await db.RunTransactionAsync(async transaction =>
            {
                formed = false;
                matchedUids.Clear();
                displayNames.Clear();

                var query = db.Collection(QueueCollection)
                    .WhereEqualTo("bucketId", bucketId)
                    .WhereEqualTo("status", "queued")
                    .Limit(12);
                var snap = await query.GetSnapshotAsync();
                if (snap == null || snap.Count < 4)
                {
                    return;
                }

                var ordered = new List<global::Firebase.Firestore.DocumentSnapshot>();
                foreach (var doc in snap.Documents)
                {
                    ordered.Add(doc);
                }

                ordered.Sort((a, b) =>
                {
                    var aAt = MatchStateFirestoreCodec.GetDouble(a.ToDictionary(), "queuedAt", 0);
                    var bAt = MatchStateFirestoreCodec.GetDouble(b.ToDictionary(), "queuedAt", 0);
                    return aAt.CompareTo(bAt);
                });

                for (var i = 0; i < ordered.Count && matchedUids.Count < 4; i++)
                {
                    var doc = ordered[i];
                    matchedUids.Add(doc.Id);
                    var data = doc.ToDictionary();
                    displayNames[doc.Id] = MatchStateFirestoreCodec.GetString(data, "displayName", "Player");
                }

                if (matchedUids.Count < 4)
                {
                    return;
                }

                hostUid = matchedUids[0];
                var tableRef = db.Collection(TableSessionService.CollectionName).Document();
                tableId = tableRef.Id;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var seats = new Dictionary<string, object>
                {
                    [SeatId.Bottom.ToString()] = SeatMap(matchedUids[0], displayNames[matchedUids[0]]),
                    [SeatId.Top.ToString()] = SeatMap(matchedUids[1], displayNames[matchedUids[1]]),
                    [SeatId.Left.ToString()] = SeatMap(matchedUids[2], displayNames[matchedUids[2]]),
                    [SeatId.Right.ToString()] = SeatMap(matchedUids[3], displayNames[matchedUids[3]])
                };

                transaction.Set(tableRef, new Dictionary<string, object>
                {
                    ["status"] = "waiting_lobby",
                    ["mode"] = mode,
                    ["targetScore"] = targetScore,
                    ["ranked"] = false,
                    ["hostUid"] = hostUid,
                    ["joinCode"] = string.Empty,
                    ["sessionKey"] = Guid.NewGuid().ToString("N"),
                    ["hostLeaseAt"] = now,
                    ["matchmade"] = true,
                    ["seats"] = seats,
                    ["createdAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp,
                    ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp,
                    ["ruleSet"] = new Dictionary<string, object>
                    {
                        ["displayName"] = rules?.DisplayName ?? mode,
                        ["targetScore"] = rules?.TargetScore ?? targetScore,
                        ["spadesMustBeBroken"] = rules?.SpadesMustBeBroken ?? true,
                        ["allowSpadesAnytime"] = rules?.AllowSpadesAnytime ?? false,
                        ["followSuitRequired"] = rules?.FollowSuitRequired ?? true,
                        ["renegePenaltyEnabled"] = rules?.RenegePenaltyEnabled ?? false
                    }
                });

                for (var i = 0; i < matchedUids.Count; i++)
                {
                    var uid = matchedUids[i];
                    var ticketRef = db.Collection(QueueCollection).Document(uid);
                    transaction.Update(ticketRef, new Dictionary<string, object>
                    {
                        ["status"] = "matched",
                        ["tableId"] = tableId,
                        ["isHost"] = uid == hostUid,
                        ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
                    });
                }

                formed = true;
            });

            if (formed)
            {
                Debug.Log($"Quick Match formed table={tableId} host={hostUid} players={string.Join(",", matchedUids)}");
            }
        }

        private static Dictionary<string, object> SeatMap(string uid, string displayName)
        {
            return new Dictionary<string, object>
            {
                ["uid"] = uid,
                ["displayName"] = displayName ?? "Player",
                ["conn"] = "connected",
                ["ready"] = false
            };
        }

        private static QueueTicket FromSnapshot(global::Firebase.Firestore.DocumentSnapshot snapshot)
        {
            var data = snapshot.ToDictionary();
            return new QueueTicket
            {
                Uid = snapshot.Id,
                Mode = MatchStateFirestoreCodec.GetString(data, "mode", "Classic"),
                TargetScore = MatchStateFirestoreCodec.GetInt(data, "targetScore", 100),
                Region = MatchStateFirestoreCodec.GetString(data, "region", "auto"),
                Status = MatchStateFirestoreCodec.GetString(data, "status", "queued"),
                TableId = MatchStateFirestoreCodec.GetString(data, "tableId", string.Empty),
                JoinCode = MatchStateFirestoreCodec.GetString(data, "joinCode", string.Empty),
                IsHost = MatchStateFirestoreCodec.GetBool(data, "isHost", false),
                QueuedAtUnix = MatchStateFirestoreCodec.GetDouble(data, "queuedAt", 0)
            };
        }
    }
}
