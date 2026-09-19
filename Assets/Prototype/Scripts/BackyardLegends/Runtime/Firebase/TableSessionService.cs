using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    public sealed class TableSeatRecord
    {
        public string Uid = string.Empty;
        public string DisplayName = string.Empty;
        public string Conn = "connected"; // connected | grace | ai
    }

    public sealed class TableSessionRecord
    {
        public string TableId = string.Empty;
        public string Status = "waiting_relay"; // waiting_relay | in_play | paused | completed | abandoned
        public string Mode = "Classic";
        public int TargetScore = 100;
        public bool Ranked;
        public string HostUid = string.Empty;
        public string PreviousHostUid = string.Empty;
        public string JoinCode = string.Empty;
        public string SessionKey = string.Empty;
        public double HostLeaseAtUnix;
        public double HostGraceEndsAtUnix;
        public int ActionSeq;
        public Dictionary<string, TableSeatRecord> Seats = new();
        public Dictionary<string, object> PublicSnapshot;
        public Dictionary<string, object> HandBlobs;
        public RuleSetDefinition Rules;
    }

    public static class TableSessionService
    {
        public const string CollectionName = "tables";
        public const float HostHeartbeatSeconds = 10f;
        public const float HostLeaseDeadSeconds = 20f;
        public const float HostGraceSeconds = 90f;

        public static bool IsAvailable => FirebaseBootstrap.IsAvailable;

        public static async Task<TableSessionRecord> FindByJoinCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                return null;
            }

            await FirebaseBootstrap.EnsureInitializedAsync();
            if (!IsAvailable)
            {
                return null;
            }

            var code = joinCode.Trim().ToUpperInvariant();
            var db = FirebaseBootstrap.GetFirestore();
            if (db == null)
            {
                return null;
            }

            var query = db.Collection(CollectionName)
                .WhereEqualTo("joinCode", code)
                .Limit(1);
            var snap = await query.GetSnapshotAsync();
            if (snap == null || snap.Count == 0)
            {
                // Also try exact (LOCAL / mixed case) match.
                query = db.Collection(CollectionName).WhereEqualTo("joinCode", joinCode.Trim()).Limit(1);
                snap = await query.GetSnapshotAsync();
            }

            if (snap == null || snap.Count == 0)
            {
                return null;
            }

            foreach (var document in snap.Documents)
            {
                return FromSnapshot(document);
            }

            return null;
        }

        public static async Task<TableSessionRecord> CreateTableAsync(
            string hostUid,
            string displayName,
            RuleSetDefinition rules,
            string joinCode,
            string sessionKey)
        {
            if (string.IsNullOrWhiteSpace(hostUid))
            {
                return null;
            }

            await FirebaseBootstrap.EnsureInitializedAsync();
            if (!IsAvailable)
            {
                return null;
            }

            var db = FirebaseBootstrap.GetFirestore();
            if (db == null)
            {
                return null;
            }

            var doc = db.Collection(CollectionName).Document();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var record = new TableSessionRecord
            {
                TableId = doc.Id,
                Status = "waiting_relay",
                Mode = rules?.DisplayName ?? "Classic",
                TargetScore = rules?.TargetScore ?? 100,
                HostUid = hostUid,
                JoinCode = (joinCode ?? string.Empty).Trim().ToUpperInvariant(),
                SessionKey = string.IsNullOrWhiteSpace(sessionKey) ? Guid.NewGuid().ToString("N") : sessionKey,
                HostLeaseAtUnix = now,
                Rules = rules
            };
            record.Seats[SeatId.Bottom.ToString()] = new TableSeatRecord
            {
                Uid = hostUid,
                DisplayName = displayName ?? "Host",
                Conn = "connected"
            };

            await doc.SetAsync(ToFirestoreDictionary(record, includeSnapshot: false));
            Debug.Log($"Created Firestore table {record.TableId} joinCode={record.JoinCode}");
            return record;
        }

        public static async Task UpsertSeatAsync(string tableId, SeatId seat, string uid, string displayName, string conn)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var doc = db.Collection(CollectionName).Document(tableId);
            await doc.UpdateAsync(new Dictionary<string, object>
            {
                [$"seats.{seat}.uid"] = uid ?? string.Empty,
                [$"seats.{seat}.displayName"] = displayName ?? string.Empty,
                [$"seats.{seat}.conn"] = conn ?? "connected",
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task WriteRelayAsync(string tableId, string joinCode, string hostUid)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var code = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(CollectionName).Document(tableId).UpdateAsync(new Dictionary<string, object>
            {
                ["joinCode"] = code,
                ["relay.joinCode"] = code,
                ["hostUid"] = hostUid ?? string.Empty,
                ["hostLeaseAt"] = now,
                ["status"] = "in_play",
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task HeartbeatAsync(string tableId, string hostUid)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(CollectionName).Document(tableId).UpdateAsync(new Dictionary<string, object>
            {
                ["hostUid"] = hostUid ?? string.Empty,
                ["hostLeaseAt"] = now,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task MarkPausedForHostLossAsync(string tableId, string previousHostUid)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(CollectionName).Document(tableId).UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = "paused",
                ["previousHostUid"] = previousHostUid ?? string.Empty,
                ["hostGraceEndsAt"] = now + (long)HostGraceSeconds,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task WriteAuthoritySnapshotAsync(
            string tableId,
            MatchState state,
            int actionSeq,
            IReadOnlyDictionary<SeatId, string> uidBySeat,
            string sessionKey)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId) || state == null)
            {
                return;
            }

            var snapshot = MatchStateFirestoreCodec.EncodeFullAuthorityState(state, actionSeq);
            var blobs = MatchStateFirestoreCodec.EncodeHandBlobs(state, uidBySeat, sessionKey);
            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(CollectionName).Document(tableId).UpdateAsync(new Dictionary<string, object>
            {
                ["publicSnapshot"] = snapshot,
                ["handBlobs"] = blobs,
                ["actionSeq"] = actionSeq,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task MarkCompletedAsync(string tableId, TeamId? winningTeam)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var db = FirebaseBootstrap.GetFirestore();
            await db.Collection(CollectionName).Document(tableId).UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = "completed",
                ["result.winner"] = winningTeam.HasValue ? (int)winningTeam.Value : -1,
                ["endedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            });
        }

        public static async Task<TableSessionRecord> GetTableAsync(string tableId)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return null;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var snap = await db.Collection(CollectionName).Document(tableId).GetSnapshotAsync();
            if (!snap.Exists)
            {
                return null;
            }

            return FromSnapshot(snap);
        }

        public static ListenerRegistrationWrapper Listen(
            string tableId,
            Action<TableSessionRecord> onChanged,
            Action<Exception> onError = null)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return null;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var registration = db.Collection(CollectionName).Document(tableId).Listen(snapshot =>
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

        /// <summary>
        /// Seat-order client promotion: claim host if lease dead and grace expired (or already paused past grace).
        /// </summary>
        public static async Task<bool> TryClaimHostAsync(string tableId, string claimantUid, SeatId claimantSeat)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(tableId) || string.IsNullOrWhiteSpace(claimantUid))
            {
                return false;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var doc = db.Collection(CollectionName).Document(tableId);
            var claimed = false;
            await db.RunTransactionAsync(async transaction =>
            {
                var snap = await transaction.GetSnapshotAsync(doc);
                if (!snap.Exists)
                {
                    return;
                }

                var data = snap.ToDictionary();
                var hostUid = MatchStateFirestoreCodec.GetString(data, "hostUid", string.Empty);
                var status = MatchStateFirestoreCodec.GetString(data, "status", string.Empty);
                var leaseAt = MatchStateFirestoreCodec.GetDouble(data, "hostLeaseAt", 0);
                var graceEnds = MatchStateFirestoreCodec.GetDouble(data, "hostGraceEndsAt", 0);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var leaseDead = now - leaseAt >= HostLeaseDeadSeconds;
                var graceOver = graceEnds <= 0 || now >= graceEnds;

                if (!leaseDead)
                {
                    return;
                }

                if (status == "completed" || status == "abandoned")
                {
                    return;
                }

                // Someone already claimed after grace.
                if (!string.IsNullOrEmpty(hostUid) &&
                    !string.Equals(hostUid, MatchStateFirestoreCodec.GetString(data, "previousHostUid", hostUid), StringComparison.Ordinal) &&
                    now - leaseAt < HostLeaseDeadSeconds)
                {
                    return;
                }

                if (!graceOver && !string.Equals(claimantUid, MatchStateFirestoreCodec.GetString(data, "previousHostUid", string.Empty), StringComparison.Ordinal) &&
                    !string.Equals(claimantUid, MatchStateFirestoreCodec.GetString(data, "hostUid", string.Empty), StringComparison.Ordinal))
                {
                    // During grace only original host may reclaim.
                    var previous = MatchStateFirestoreCodec.GetString(data, "previousHostUid", string.Empty);
                    if (string.IsNullOrEmpty(previous))
                    {
                        previous = hostUid;
                    }

                    if (!string.Equals(claimantUid, previous, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                transaction.Update(doc, new Dictionary<string, object>
                {
                    ["previousHostUid"] = hostUid,
                    ["hostUid"] = claimantUid,
                    ["hostLeaseAt"] = now,
                    ["status"] = "waiting_relay",
                    ["promotedSeat"] = (int)claimantSeat,
                    ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
                });
                claimed = true;
            });

            return claimed;
        }

        public static bool IsHostLeaseDead(TableSessionRecord table)
        {
            if (table == null)
            {
                return true;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now - table.HostLeaseAtUnix >= HostLeaseDeadSeconds;
        }

        public static bool IsHostGraceOver(TableSessionRecord table)
        {
            if (table == null)
            {
                return true;
            }

            if (table.HostGraceEndsAtUnix <= 0)
            {
                return IsHostLeaseDead(table);
            }

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= table.HostGraceEndsAtUnix;
        }

        private static Dictionary<string, object> ToFirestoreDictionary(TableSessionRecord record, bool includeSnapshot)
        {
            var seats = new Dictionary<string, object>();
            foreach (var pair in record.Seats)
            {
                seats[pair.Key] = new Dictionary<string, object>
                {
                    ["uid"] = pair.Value.Uid ?? string.Empty,
                    ["displayName"] = pair.Value.DisplayName ?? string.Empty,
                    ["conn"] = pair.Value.Conn ?? "connected"
                };
            }

            var data = new Dictionary<string, object>
            {
                ["status"] = record.Status,
                ["mode"] = record.Mode,
                ["targetScore"] = record.TargetScore,
                ["ranked"] = record.Ranked,
                ["hostUid"] = record.HostUid,
                ["previousHostUid"] = record.PreviousHostUid ?? string.Empty,
                ["joinCode"] = record.JoinCode,
                ["sessionKey"] = record.SessionKey,
                ["hostLeaseAt"] = record.HostLeaseAtUnix,
                ["hostGraceEndsAt"] = record.HostGraceEndsAtUnix,
                ["actionSeq"] = record.ActionSeq,
                ["relay"] = new Dictionary<string, object> { ["joinCode"] = record.JoinCode },
                ["seats"] = seats,
                ["createdAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp,
                ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
            };

            if (record.Rules != null)
            {
                data["ruleSet"] = new Dictionary<string, object>
                {
                    ["displayName"] = record.Rules.DisplayName ?? "Classic",
                    ["targetScore"] = record.Rules.TargetScore,
                    ["spadesMustBeBroken"] = record.Rules.SpadesMustBeBroken,
                    ["allowSpadesAnytime"] = record.Rules.AllowSpadesAnytime,
                    ["followSuitRequired"] = record.Rules.FollowSuitRequired,
                    ["renegePenaltyEnabled"] = record.Rules.RenegePenaltyEnabled
                };
            }

            if (includeSnapshot && record.PublicSnapshot != null)
            {
                data["publicSnapshot"] = record.PublicSnapshot;
            }

            return data;
        }

        private static TableSessionRecord FromSnapshot(global::Firebase.Firestore.DocumentSnapshot snapshot)
        {
            var data = snapshot.ToDictionary();
            var record = new TableSessionRecord
            {
                TableId = snapshot.Id,
                Status = MatchStateFirestoreCodec.GetString(data, "status", "waiting_relay"),
                Mode = MatchStateFirestoreCodec.GetString(data, "mode", "Classic"),
                TargetScore = MatchStateFirestoreCodec.GetInt(data, "targetScore", 100),
                Ranked = MatchStateFirestoreCodec.GetBool(data, "ranked", false),
                HostUid = MatchStateFirestoreCodec.GetString(data, "hostUid", string.Empty),
                PreviousHostUid = MatchStateFirestoreCodec.GetString(data, "previousHostUid", string.Empty),
                JoinCode = MatchStateFirestoreCodec.GetString(data, "joinCode", string.Empty),
                SessionKey = MatchStateFirestoreCodec.GetString(data, "sessionKey", string.Empty),
                HostLeaseAtUnix = MatchStateFirestoreCodec.GetDouble(data, "hostLeaseAt", 0),
                HostGraceEndsAtUnix = MatchStateFirestoreCodec.GetDouble(data, "hostGraceEndsAt", 0),
                ActionSeq = MatchStateFirestoreCodec.GetInt(data, "actionSeq", 0),
                PublicSnapshot = MatchStateFirestoreCodec.GetMap(data, "publicSnapshot") as Dictionary<string, object>
                    ?? CopyMap(MatchStateFirestoreCodec.GetMap(data, "publicSnapshot")),
                HandBlobs = MatchStateFirestoreCodec.GetMap(data, "handBlobs") as Dictionary<string, object>
                    ?? CopyMap(MatchStateFirestoreCodec.GetMap(data, "handBlobs"))
            };

            if (string.IsNullOrEmpty(record.JoinCode))
            {
                var relay = MatchStateFirestoreCodec.GetMap(data, "relay");
                record.JoinCode = MatchStateFirestoreCodec.GetString(relay, "joinCode", string.Empty);
            }

            var seats = MatchStateFirestoreCodec.GetMap(data, "seats");
            if (seats != null)
            {
                foreach (var pair in seats)
                {
                    var seatMap = pair.Value as IDictionary<string, object> ?? CopyMap(pair.Value as IDictionary<string, object>);
                    if (pair.Value is System.Collections.IDictionary dict && seatMap == null)
                    {
                        seatMap = new Dictionary<string, object>();
                        foreach (System.Collections.DictionaryEntry entry in dict)
                        {
                            seatMap[entry.Key.ToString()] = entry.Value;
                        }
                    }

                    record.Seats[pair.Key] = new TableSeatRecord
                    {
                        Uid = MatchStateFirestoreCodec.GetString(seatMap, "uid", string.Empty),
                        DisplayName = MatchStateFirestoreCodec.GetString(seatMap, "displayName", string.Empty),
                        Conn = MatchStateFirestoreCodec.GetString(seatMap, "conn", "connected")
                    };
                }
            }

            var ruleMap = MatchStateFirestoreCodec.GetMap(data, "ruleSet");
            if (ruleMap != null)
            {
                record.Rules = new RuleSetDefinition
                {
                    DisplayName = MatchStateFirestoreCodec.GetString(ruleMap, "displayName", record.Mode),
                    TargetScore = MatchStateFirestoreCodec.GetInt(ruleMap, "targetScore", record.TargetScore),
                    SpadesMustBeBroken = MatchStateFirestoreCodec.GetBool(ruleMap, "spadesMustBeBroken", true),
                    AllowSpadesAnytime = MatchStateFirestoreCodec.GetBool(ruleMap, "allowSpadesAnytime", false),
                    FollowSuitRequired = MatchStateFirestoreCodec.GetBool(ruleMap, "followSuitRequired", true),
                    RenegePenaltyEnabled = MatchStateFirestoreCodec.GetBool(ruleMap, "renegePenaltyEnabled", false)
                };
            }

            return record;
        }

        private static Dictionary<string, object> CopyMap(IDictionary<string, object> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new Dictionary<string, object>();
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }

    public sealed class ListenerRegistrationWrapper : IDisposable
    {
        private readonly global::Firebase.Firestore.ListenerRegistration registration;

        public ListenerRegistrationWrapper(global::Firebase.Firestore.ListenerRegistration registration)
        {
            this.registration = registration;
        }

        public void Dispose()
        {
            registration?.Stop();
        }
    }
}
