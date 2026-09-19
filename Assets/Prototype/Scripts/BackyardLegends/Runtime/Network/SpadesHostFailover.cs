using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Firebase;
using Unity.Netcode;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    /// <summary>
    /// Detects dead host lease, waits 90s for original host return, then promotes by seat order.
    /// Requires Firestore table docs; no-ops when Firebase is unavailable.
    /// </summary>
    public sealed class SpadesHostFailover : MonoBehaviour
    {
        public static SpadesHostFailover Instance { get; private set; }

        private ListenerRegistrationWrapper tableListener;
        private Coroutine failoverRoutine;
        private bool failoverInFlight;
        private TableSessionRecord latestTable;
        private bool pausedForHostLoss;

        public event Action<string> HostFailoverStatus;
        public event Action TablePausedForHostLoss;
        public event Action TableResumedAfterHostFailover;

        public static SpadesHostFailover GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<SpadesHostFailover>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("Spades Host Failover");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SpadesHostFailover>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void BeginWatchingTable(string tableId)
        {
            StopWatching();
            if (string.IsNullOrWhiteSpace(tableId) || !TableSessionService.IsAvailable)
            {
                return;
            }

            tableListener = TableSessionService.Listen(tableId, HandleTableChanged, ex =>
            {
                Debug.LogWarning($"Table listen failed: {ex.Message}");
            });
        }

        public void StopWatching()
        {
            tableListener?.Dispose();
            tableListener = null;
            if (failoverRoutine != null)
            {
                StopCoroutine(failoverRoutine);
                failoverRoutine = null;
            }

            failoverInFlight = false;
            pausedForHostLoss = false;
        }

        public void NotifyPossibleHostLoss()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (string.IsNullOrWhiteSpace(session.TableId) || !session.MatchWasLive)
            {
                return;
            }

            if (failoverInFlight)
            {
                return;
            }

            if (failoverRoutine != null)
            {
                StopCoroutine(failoverRoutine);
            }

            failoverRoutine = StartCoroutine(HostLossRoutine());
        }

        private void HandleTableChanged(TableSessionRecord table)
        {
            latestTable = table;
            var session = SpadesNetworkSession.GetOrCreate();
            if (table == null || string.IsNullOrWhiteSpace(session.TableId))
            {
                return;
            }

            if (!string.IsNullOrEmpty(table.JoinCode) &&
                !string.Equals(table.JoinCode, session.JoinCode, StringComparison.OrdinalIgnoreCase) &&
                pausedForHostLoss &&
                !TableSessionService.IsHostLeaseDead(table))
            {
                // New host published Relay — clients should rejoin.
                StartCoroutine(RejoinNewHostRoutine(table.JoinCode));
            }
        }

        private IEnumerator HostLossRoutine()
        {
            failoverInFlight = true;
            var session = SpadesNetworkSession.GetOrCreate();
            RaiseStatus("Host connection lost — checking table…");

            if (!TableSessionService.IsAvailable)
            {
                RaiseStatus("Host lost (no Firebase table). Match cannot migrate.");
                failoverInFlight = false;
                yield break;
            }

            // Mark paused (any client may write; best-effort).
            var markTask = TableSessionService.MarkPausedForHostLossAsync(session.TableId, session.LastKnownHostUid);
            while (!markTask.IsCompleted)
            {
                yield return null;
            }

            pausedForHostLoss = true;
            TablePausedForHostLoss?.Invoke();
            RaiseStatus("Table paused — waiting for host (90s)…");

            var graceEnds = Time.realtimeSinceStartup + TableSessionService.HostGraceSeconds;
            while (Time.realtimeSinceStartup < graceEnds)
            {
                var refresh = TableSessionService.GetTableAsync(session.TableId);
                while (!refresh.IsCompleted)
                {
                    yield return null;
                }

                if (refresh.Status == TaskStatus.RanToCompletion)
                {
                    latestTable = refresh.Result;
                }

                if (latestTable != null && !TableSessionService.IsHostLeaseDead(latestTable))
                {
                    // Original or new host heartbeating again.
                    if (!string.IsNullOrEmpty(latestTable.JoinCode))
                    {
                        RaiseStatus("Host returned — rejoining…");
                        yield return RejoinNewHostRoutine(latestTable.JoinCode);
                    }

                    failoverInFlight = false;
                    pausedForHostLoss = false;
                    TableResumedAfterHostFailover?.Invoke();
                    yield break;
                }

                // Original host reclaim attempt (if this device was the host).
                if (string.Equals(session.LocalPlayerId, session.LastKnownHostUid, StringComparison.Ordinal))
                {
                    yield return TryBecomeHostRoutine(isOriginalHost: true);
                    if (session.Role == SpadesNetworkRole.Host)
                    {
                        failoverInFlight = false;
                        pausedForHostLoss = false;
                        TableResumedAfterHostFailover?.Invoke();
                        yield break;
                    }
                }

                var remaining = Mathf.CeilToInt(graceEnds - Time.realtimeSinceStartup);
                RaiseStatus($"Waiting for host… {Mathf.Max(0, remaining)}s");
                yield return new WaitForSecondsRealtime(2f);
            }

            RaiseStatus("Host grace expired — promoting…");
            yield return TryBecomeHostRoutine(isOriginalHost: false);

            // If we did not become host, wait for join code from promoter.
            var waitPromo = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < waitPromo && session.Role != SpadesNetworkRole.Host)
            {
                var refresh = TableSessionService.GetTableAsync(session.TableId);
                while (!refresh.IsCompleted)
                {
                    yield return null;
                }

                latestTable = refresh.Status == TaskStatus.RanToCompletion ? refresh.Result : latestTable;
                if (latestTable != null &&
                    !TableSessionService.IsHostLeaseDead(latestTable) &&
                    !string.IsNullOrEmpty(latestTable.JoinCode) &&
                    !string.Equals(latestTable.HostUid, session.LastKnownHostUid, StringComparison.Ordinal))
                {
                    yield return RejoinNewHostRoutine(latestTable.JoinCode);
                    break;
                }

                yield return new WaitForSecondsRealtime(2f);
            }

            failoverInFlight = false;
            pausedForHostLoss = false;
            TableResumedAfterHostFailover?.Invoke();
        }

        private IEnumerator TryBecomeHostRoutine(bool isOriginalHost)
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (!session.SeatAssigned)
            {
                yield break;
            }

            // Stagger non-original claimants by seat to reduce races.
            if (!isOriginalHost)
            {
                var delay = (int)session.LocalLogicalSeat * 2f;
                yield return new WaitForSecondsRealtime(delay);
            }

            var claimTask = TableSessionService.TryClaimHostAsync(
                session.TableId,
                session.LocalPlayerId,
                session.LocalLogicalSeat);
            while (!claimTask.IsCompleted)
            {
                yield return null;
            }

            if (claimTask.Status != TaskStatus.RanToCompletion || !claimTask.Result)
            {
                yield break;
            }

            RaiseStatus("Claimed host — restoring table…");
            yield return PromoteLocalToHostRoutine();
        }

        private IEnumerator PromoteLocalToHostRoutine()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            var tableTask = TableSessionService.GetTableAsync(session.TableId);
            while (!tableTask.IsCompleted)
            {
                yield return null;
            }

            var table = tableTask.Status == TaskStatus.RanToCompletion ? tableTask.Result : null;
            if (table == null)
            {
                RaiseStatus("Promote failed: missing table snapshot.");
                yield break;
            }

            var rules = table.Rules ?? session.PendingRules ?? BackyardLegendsSession.GetOrCreateRuntimeInstance().SelectedRule;
            var state = table.PublicSnapshot != null
                ? MatchStateFirestoreCodec.DecodeAuthorityState(table.PublicSnapshot, rules)
                : null;
            if (state != null && table.HandBlobs != null)
            {
                MatchStateFirestoreCodec.TryApplyHandBlobs(state, table.HandBlobs, table.SessionKey);
            }

            if (state == null)
            {
                RaiseStatus("Promote failed: empty authority snapshot.");
                yield break;
            }

            // Tear down old client NGO session and start hosting.
            SpadesNetworkManagerHost.GetOrCreate().ShutdownNetworkKeepingSession();
            session.BeginHost(rules);
            session.SetTableSession(table.TableId, table.SessionKey, session.LocalPlayerId);
            session.SetPendingRestoreState(state, table.ActionSeq);
            if (table.Seats != null && table.Seats.Count > 0)
            {
                var roster = new Dictionary<SeatId, (string Uid, string DisplayName)>();
                foreach (var pair in table.Seats)
                {
                    if (!Enum.TryParse(pair.Key, true, out SeatId seat))
                    {
                        continue;
                    }

                    roster[seat] = (pair.Value.Uid ?? string.Empty, pair.Value.DisplayName ?? seat.ToString());
                }

                session.SetPendingSeatRoster(roster);
            }

            session.AutoReconnectEnabled = false;

            var host = SpadesNetworkManagerHost.GetOrCreate();
            var startTask = host.StartHostAsync();
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            if (startTask.Status != TaskStatus.RanToCompletion || !startTask.Result)
            {
                RaiseStatus("Promote failed: could not start host.");
                yield break;
            }

            var writeRelay = TableSessionService.WriteRelayAsync(session.TableId, session.JoinCode, session.LocalPlayerId);
            while (!writeRelay.IsCompleted)
            {
                yield return null;
            }

            host.SpawnTableIfHost();
            RaiseStatus($"Promoted host · code {session.JoinCode}");
            TableResumedAfterHostFailover?.Invoke();
        }

        private IEnumerator RejoinNewHostRoutine(string joinCode)
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (session.Role == SpadesNetworkRole.Host)
            {
                yield break;
            }

            RaiseStatus($"Rejoining new host ({joinCode})…");
            SpadesNetworkManagerHost.GetOrCreate().ShutdownNetworkKeepingSession();
            session.BeginClient(joinCode, session.PendingRules);
            session.MatchWasLive = true;
            session.AutoReconnectEnabled = true;

            var joinTask = SpadesNetworkManagerHost.GetOrCreate().StartClientAsync(joinCode, preserveSession: true);
            while (!joinTask.IsCompleted)
            {
                yield return null;
            }

            if (joinTask.Status == TaskStatus.RanToCompletion && joinTask.Result)
            {
                RaiseStatus("Rejoined after host failover.");
                pausedForHostLoss = false;
            }
            else
            {
                RaiseStatus("Failed to rejoin new host.");
            }
        }

        private void RaiseStatus(string message)
        {
            SpadesNetworkSession.GetOrCreate().SetStatus(message);
            HostFailoverStatus?.Invoke(message);
            Debug.Log($"[HostFailover] {message}");
        }

        private void OnDestroy()
        {
            StopWatching();
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
