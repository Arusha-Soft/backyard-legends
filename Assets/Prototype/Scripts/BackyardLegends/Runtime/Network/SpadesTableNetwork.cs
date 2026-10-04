using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Firebase;
using Unity.Netcode;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesTableNetwork : NetworkBehaviour
    {
        private const float ReconnectGraceSeconds = 90f;

        public static SpadesTableNetwork Instance { get; private set; }

        private readonly Dictionary<ulong, SeatId> seatByClient = new();
        private readonly Dictionary<SeatId, ulong> clientBySeat = new();
        private readonly Dictionary<ulong, string> uidByClient = new();
        private readonly Dictionary<string, SeatId> seatByUid = new();
        private readonly Dictionary<SeatId, string> uidBySeat = new();
        private readonly Dictionary<SeatId, string> displayNameBySeat = new();
        private readonly HashSet<SeatId> humanOwnedSeats = new();
        private readonly HashSet<SeatId> connectedHumanSeats = new();
        private readonly HashSet<SeatId> aiSitInSeats = new();
        private readonly Dictionary<SeatId, float> graceEndsAtBySeat = new();
        private readonly HashSet<SeatId> readyForNextHand = new();
        private readonly HashSet<SeatId> readyForMatch = new();
        private readonly HashSet<SeatId> gracePendingSeats = new();
        private readonly Dictionary<ulong, List<Card>> privateHandsByClient = new();
        private readonly HashSet<ulong> pendingRegistration = new();

        private readonly Dictionary<string, SeatId> preferredSeatByUid = new();
        private bool preferredSeatsLoaded;

        private SpadesMatchController hostController;
        private SpadesRuleEngine ruleEngine;
        private bool matchStarted;
        private float autoStartAt = -1f;
        private bool requireFullHumanLobby;
        private List<Card> localPrivateHand = new();
        private bool localPlayerRegistered;
        private int actionSeq;
        private Coroutine heartbeatRoutine;

        public SpadesMatchController HostController => hostController;
        public bool MatchStarted => matchStarted;
        public bool IsWaitingInLobby => !matchStarted && requireFullHumanLobby;
        public IReadOnlyCollection<SeatId> ReadySeats => readyForMatch;
        public IReadOnlyDictionary<SeatId, string> DisplayNames => displayNameBySeat;
        public IReadOnlyCollection<SeatId> OccupiedHumanSeats => humanOwnedSeats;
        public IReadOnlyList<Card> LocalPrivateHand => localPrivateHand;

        public event Action<SpadesNetworkEventPayload> NetworkEventReceived;
        public event Action<SeatId> LocalSeatAssigned;
        public event Action MatchBound;
        public event Action PrivateHandUpdated;
        public event Action LobbyStateChanged;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            // Client replicas are not covered by host-side DontDestroyOnLoad — survive LoadScene(Single).
            DontDestroyOnLoad(gameObject);
            if (IsServer)
            {
                BindExistingClients();
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                var session = SpadesNetworkSession.GetOrCreate();
                requireFullHumanLobby = session.RequireFullHumanLobby || session.IsOnline;
                if (requireFullHumanLobby)
                {
                    autoStartAt = -1f;
                    session.SetStatus("Private room lobby — waiting for 4 players…");
                }
                else
                {
                    // Legacy editor path: AI fill after wait when not in full-lobby mode.
                    autoStartAt = Time.time + 45f;
                    session.SetStatus("Waiting for players (AI fills in 45s)…");
                }
            }

            TryRegisterLocalPlayer();
            if (IsServer)
            {
                SyncTableSessionToClients();
            }
            else
            {
                // Fallback: resolve tableId from join code if ClientRpc was missed.
                StartCoroutine(ResolveTableSessionFromJoinCodeRoutine());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (hostController != null)
            {
                hostController.EventRaised -= HandleHostMatchEvent;
            }

            if (heartbeatRoutine != null)
            {
                StopCoroutine(heartbeatRoutine);
                heartbeatRoutine = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            TickGraceExpiry();

            if (!matchStarted && !requireFullHumanLobby && autoStartAt >= 0f && Time.time >= autoStartAt)
            {
                TryStartMatchWithAiFill();
            }
        }

        private void TickGraceExpiry()
        {
            if (!matchStarted || gracePendingSeats.Count == 0)
            {
                return;
            }

            var expired = new List<SeatId>();
            foreach (var seat in gracePendingSeats)
            {
                if (!graceEndsAtBySeat.TryGetValue(seat, out var endsAt) || Time.time < endsAt)
                {
                    continue;
                }

                expired.Add(seat);
            }

            foreach (var seat in expired)
            {
                gracePendingSeats.Remove(seat);
                ActivateAiSitInAfterGrace(seat);
            }

            if (gracePendingSeats.Count == 0 && matchStarted)
            {
                BroadcastTableEvent(SpadesNetworkEventKind.TableResumed, "Table resumed");
                AdvanceAiUntilHumanOrIdle();
            }
        }

        private void BindExistingClients()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                pendingRegistration.Add(clientId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            pendingRegistration.Add(clientId);
            Debug.Log($"Client connected id={clientId} total={NetworkManager.ConnectedClientsIds.Count}");
            if (!matchStarted)
            {
                SpadesNetworkSession.GetOrCreate().SetStatus(
                    requireFullHumanLobby
                        ? $"Lobby {NetworkManager.ConnectedClientsIds.Count}/4 — ready up to start"
                        : $"Players connected: {NetworkManager.ConnectedClientsIds.Count}/4");
                BroadcastLobbyRoster();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            pendingRegistration.Remove(clientId);
            uidByClient.Remove(clientId);
            privateHandsByClient.Remove(clientId);

            if (!seatByClient.TryGetValue(clientId, out var seat))
            {
                return;
            }

            seatByClient.Remove(clientId);
            clientBySeat.Remove(seat);
            connectedHumanSeats.Remove(seat);
            readyForNextHand.Remove(seat);
            readyForMatch.Remove(seat);

            if (!matchStarted || hostController == null)
            {
                humanOwnedSeats.Remove(seat);
                if (uidBySeat.TryGetValue(seat, out var uid))
                {
                    seatByUid.Remove(uid);
                    uidBySeat.Remove(seat);
                }

                displayNameBySeat.Remove(seat);
                var session = SpadesNetworkSession.GetOrCreate();
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    _ = TableSessionService.ClearSeatAsync(session.TableId, seat);
                }

                BroadcastLobbyRoster();
                LobbyStateChanged?.Invoke();
                return;
            }

            // Host listen-server process owns the table; if the host client drops, session dies.
            if (clientId == NetworkManager.LocalClientId)
            {
                return;
            }

            BeginGracePeriod(seat, "disconnected");
        }

        public void EnsureLocalPlayerRegistered()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (session.SeatAssigned && localPlayerRegistered)
            {
                return;
            }

            // Allow re-register when session was recreated after LoadScene.
            localPlayerRegistered = false;
            TryRegisterLocalPlayer();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPresentationSyncServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!seatByClient.TryGetValue(clientId, out var seat))
            {
                return;
            }

            var displayName = displayNameBySeat.TryGetValue(seat, out var name) ? name : $"Player {clientId}";
            var seatPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.SeatAssigned,
                Seat = (byte)seat,
                Message = displayName
            };
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            SendEventClientRpc(seatPayload, target);

            if (matchStarted && hostController != null)
            {
                SendCatchUpToClient(clientId, seat);
            }
        }

        private void TryRegisterLocalPlayer()
        {
            if (!IsClient && !IsHost)
            {
                return;
            }

            if (localPlayerRegistered)
            {
                return;
            }

            var session = SpadesNetworkSession.GetOrCreate();
            ResolveAndStoreLocalIdentity(session);
            RegisterPlayerServerRpc(session.LocalPlayerId, session.LocalDisplayName);
            localPlayerRegistered = true;
        }

        private static void ResolveAndStoreLocalIdentity(SpadesNetworkSession session)
        {
            session.EnsureLocalPlayerId();
            var backyard = BackyardLegendsSession.Instance;
            var uid = session.LocalPlayerId;
            var displayName = session.LocalDisplayName;
            if (backyard?.CurrentUser != null && backyard.CurrentUser.IsSignedIn)
            {
                if (!string.IsNullOrWhiteSpace(backyard.CurrentUser.Uid))
                {
                    uid = backyard.CurrentUser.Uid;
                }

                if (!string.IsNullOrWhiteSpace(backyard.CurrentUser.DisplayName))
                {
                    displayName = backyard.CurrentUser.DisplayName;
                }
            }

            session.SetLocalPlayerIdentity(uid, displayName);
#if UNITY_EDITOR
            session.SetLocalPlayerIdentity(
                SpadesNetworkSession.ApplyEditorCloneUidSuffix(session.LocalPlayerId),
                session.LocalDisplayName);
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerServerRpc(string uid, string displayName, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            pendingRegistration.Remove(clientId);

            if (string.IsNullOrWhiteSpace(uid))
            {
                Reject(clientId, "Missing player id.");
                return;
            }

            uid = uid.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? $"Player {clientId}" : displayName.Trim();
            uidByClient[clientId] = uid;

            if (seatByClient.ContainsKey(clientId))
            {
                return;
            }

            if (matchStarted)
            {
                if (seatByUid.TryGetValue(uid, out var reclaimSeat) &&
                    humanOwnedSeats.Contains(reclaimSeat) &&
                    aiSitInSeats.Contains(reclaimSeat))
                {
                    ReclaimSeat(clientId, reclaimSeat, displayName);
                    return;
                }

                // Take over a pure AI-fill seat (never human-owned) so late ParrelSync joins
                // still replace Top/Left/Right AI instead of being rejected mid-lobby race.
                var aiSeat = PickAiFillSeat();
                if (aiSeat.HasValue)
                {
                    TakeOverAiFillSeat(clientId, aiSeat.Value, uid, displayName);
                    return;
                }

                Reject(clientId, "Match already in progress. Ask the host for a new private room code after this hand.");
                if (NetworkManager != null)
                {
                    NetworkManager.DisconnectClient(clientId);
                }

                return;
            }

            // Same Firebase/local uid already seated (ParrelSync shared prefs) → still give a free seat.
            if (seatByUid.TryGetValue(uid, out var existingSeat) &&
                clientBySeat.TryGetValue(existingSeat, out var existingClient) &&
                existingClient != clientId)
            {
                uid = $"{uid}#c{clientId}";
                uidByClient[clientId] = uid;
                if (string.Equals(displayName, "You", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Player {clientId}";
                }
            }

            AssignSeatToClient(clientId, uid, displayName);
            if (!matchStarted && !requireFullHumanLobby)
            {
                ExtendAutoStart(5f);
            }

            if (!matchStarted)
            {
                BroadcastLobbyRoster();
                TryAutoStartWhenLobbyReady();
            }

            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId))
            {
                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                };
                BroadcastTableSessionClientRpc(session.TableId, session.SessionKey, session.LastKnownHostUid, target);
            }
        }

        private void ExtendAutoStart(float extraSeconds)
        {
            if (matchStarted)
            {
                return;
            }

            var proposed = Time.time + Mathf.Max(0.5f, extraSeconds);
            if (autoStartAt < 0f || proposed > autoStartAt)
            {
                autoStartAt = proposed;
            }
        }

        private void AssignSeatToClient(ulong clientId, string uid, string displayName)
        {
            if (seatByClient.ContainsKey(clientId))
            {
                return;
            }

            SeatId? seat = null;
            if (preferredSeatByUid.TryGetValue(uid, out var preferred) &&
                !clientBySeat.ContainsKey(preferred) &&
                !humanOwnedSeats.Contains(preferred))
            {
                seat = preferred;
            }

            if (!seat.HasValue)
            {
                seat = PickNextSeat();
            }

            if (!seat.HasValue)
            {
                Debug.LogWarning($"No seats left for client {clientId}");
                Reject(clientId, "Table is full.");
                return;
            }

            BindClientToSeat(clientId, seat.Value, uid, displayName, sendCatchUp: false);
        }

        public void LoadPreferredSeatsFromTable(TableSessionRecord table)
        {
            preferredSeatByUid.Clear();
            preferredSeatsLoaded = false;
            if (table?.Seats == null)
            {
                return;
            }

            foreach (var pair in table.Seats)
            {
                if (!Enum.TryParse(pair.Key, true, out SeatId seat))
                {
                    continue;
                }

                var seatUid = pair.Value?.Uid;
                if (string.IsNullOrWhiteSpace(seatUid))
                {
                    continue;
                }

                preferredSeatByUid[seatUid] = seat;
            }

            preferredSeatsLoaded = preferredSeatByUid.Count > 0;
        }

        private void ReclaimSeat(ulong clientId, SeatId seat, string displayName)
        {
            if (hostController != null)
            {
                hostController.ClearAiSitIn(seat);
                if (displayNameBySeat.TryGetValue(seat, out var originalName) &&
                    !string.IsNullOrWhiteSpace(originalName))
                {
                    hostController.State.SeatNames[seat] = originalName;
                }
                else
                {
                    hostController.State.SeatNames[seat] = displayName;
                    displayNameBySeat[seat] = displayName;
                }
            }

            aiSitInSeats.Remove(seat);
            graceEndsAtBySeat.Remove(seat);
            gracePendingSeats.Remove(seat);

            var uid = uidByClient.TryGetValue(clientId, out var registeredUid)
                ? registeredUid
                : (uidBySeat.TryGetValue(seat, out var seatUid) ? seatUid : string.Empty);
            var reclaimName = displayNameBySeat.TryGetValue(seat, out var storedName) && !string.IsNullOrWhiteSpace(storedName)
                ? storedName
                : displayName;
            BindClientToSeat(clientId, seat, uid, reclaimName, sendCatchUp: true);

            var returnedPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.PlayerReturned,
                Seat = (byte)seat,
                Message = $"{hostController?.State.SeatNames[seat] ?? displayName} returned",
                PublicState = hostController != null
                    ? SpadesNetworkPublicState.FromMatchState(hostController.State)
                    : null
            };
            SendEventClientRpc(returnedPayload);
            if (gracePendingSeats.Count == 0)
            {
                BroadcastTableEvent(SpadesNetworkEventKind.TableResumed, "Table resumed");
                AdvanceAiUntilHumanOrIdle();
            }

            SpadesNetworkSession.GetOrCreate().SetStatus("Match live");
        }

        private void BindClientToSeat(ulong clientId, SeatId seat, string uid, string displayName, bool sendCatchUp)
        {
            seatByClient[clientId] = seat;
            clientBySeat[seat] = clientId;
            humanOwnedSeats.Add(seat);
            connectedHumanSeats.Add(seat);
            if (!string.IsNullOrWhiteSpace(uid))
            {
                seatByUid[uid] = seat;
                uidBySeat[seat] = uid;
            }

            displayNameBySeat[seat] = displayName;

            if (IsServer)
            {
                var session = SpadesNetworkSession.GetOrCreate();
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    _ = TableSessionService.UpsertSeatAsync(session.TableId, seat, uid, displayName, "connected");
                }
            }

            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.SeatAssigned,
                Seat = (byte)seat,
                Message = displayName
            };

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
            SendEventClientRpc(payload, target);

            if (clientId == NetworkManager.LocalClientId)
            {
                ApplyLocalSeat(seat, displayName);
            }

            if (sendCatchUp && hostController != null)
            {
                SendCatchUpToClient(clientId, seat);
            }

            if (!matchStarted)
            {
                LobbyStateChanged?.Invoke();
            }
        }

        private void SendCatchUpToClient(ulong clientId, SeatId seat)
        {
            var catchUp = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.CatchUpState,
                Seat = (byte)seat,
                Message = "Reconnected — catching up",
                PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
            };
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
            SendEventClientRpc(catchUp, target);
            PushPrivateHandToClient(clientId, seat);
        }

        public bool IsSeatReady(SeatId seat) => readyForMatch.Contains(seat);

        public int ReadyCount => readyForMatch.Count;

        public int OccupiedCount => humanOwnedSeats.Count;

        [ServerRpc(RequireOwnership = false)]
        public void SetLobbyReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            if (matchStarted)
            {
                return;
            }

            var clientId = rpcParams.Receive.SenderClientId;
            if (!seatByClient.TryGetValue(clientId, out var seat))
            {
                Reject(clientId, "Seat not assigned yet.");
                return;
            }

            if (ready)
            {
                readyForMatch.Add(seat);
            }
            else
            {
                readyForMatch.Remove(seat);
            }

            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId))
            {
                _ = TableSessionService.SetSeatReadyAsync(session.TableId, seat, ready);
            }

            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.LobbyReadyChanged,
                Seat = (byte)seat,
                Message = ready ? "ready" : "unready"
            };
            SendEventClientRpc(payload);
            BroadcastLobbyRoster();
            LobbyStateChanged?.Invoke();
            TryAutoStartWhenLobbyReady();
        }

        public void HostRequestStartMatch()
        {
            if (!IsServer || matchStarted)
            {
                return;
            }

            if (!CanStartFullLobby(out var reason))
            {
                SpadesNetworkSession.GetOrCreate().SetStatus(reason);
                return;
            }

            StartMatchFromLobby();
        }

        public void LocalSetLobbyReady(bool ready)
        {
            if (matchStarted)
            {
                return;
            }

            SetLobbyReadyServerRpc(ready);
        }

        public void LocalLeaveTable()
        {
            LeaveTableServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void LeaveTableServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var session = SpadesNetworkSession.GetOrCreate();
            var isHostClient = IsServer && clientId == NetworkManager.LocalClientId;

            if (seatByClient.TryGetValue(clientId, out var seat))
            {
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    _ = TableSessionService.LeaveOrAbandonAsync(
                        session.TableId,
                        uidByClient.TryGetValue(clientId, out var uid) ? uid : session.LocalPlayerId,
                        isHostClient,
                        seat);
                }

                if (!matchStarted)
                {
                    seatByClient.Remove(clientId);
                    clientBySeat.Remove(seat);
                    connectedHumanSeats.Remove(seat);
                    humanOwnedSeats.Remove(seat);
                    readyForMatch.Remove(seat);
                    if (uidBySeat.TryGetValue(seat, out var leaveUid))
                    {
                        seatByUid.Remove(leaveUid);
                        uidBySeat.Remove(seat);
                    }

                    displayNameBySeat.Remove(seat);
                    BroadcastLobbyRoster();
                }
            }
            else if (isHostClient && !string.IsNullOrEmpty(session.TableId))
            {
                _ = TableSessionService.MarkAbandonedAsync(session.TableId, "host_left");
            }

            if (isHostClient)
            {
                BroadcastTableEvent(SpadesNetworkEventKind.ActionRejected, "Host left — room closed");
                StartCoroutine(ShutdownAfterHostLeaveRoutine());
                return;
            }

            if (NetworkManager != null)
            {
                NetworkManager.DisconnectClient(clientId);
            }
        }

        private IEnumerator ShutdownAfterHostLeaveRoutine()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            SpadesNetworkManagerHost.GetOrCreate().Shutdown();
            BackyardLegendsSession.GetOrCreateRuntimeInstance().LoadLobbyScene();
        }

        private bool CanStartFullLobby(out string reason)
        {
            reason = string.Empty;
            if (pendingRegistration.Count > 0)
            {
                reason = "Waiting for players to finish joining…";
                return false;
            }

            if (humanOwnedSeats.Count < 4 || connectedHumanSeats.Count < 4)
            {
                reason = $"Need 4 players ({connectedHumanSeats.Count}/4 seated).";
                return false;
            }

            foreach (var seat in humanOwnedSeats)
            {
                if (!readyForMatch.Contains(seat))
                {
                    reason = $"All players must ready ({readyForMatch.Count}/4).";
                    return false;
                }
            }

            return true;
        }

        private void TryAutoStartWhenLobbyReady()
        {
            if (!requireFullHumanLobby || matchStarted)
            {
                return;
            }

            if (CanStartFullLobby(out _))
            {
                StartMatchFromLobby();
            }
            else
            {
                SpadesNetworkSession.GetOrCreate().SetStatus(
                    $"Lobby {connectedHumanSeats.Count}/4 · ready {readyForMatch.Count}/4");
            }
        }

        private void StartMatchFromLobby()
        {
            if (matchStarted)
            {
                return;
            }

            autoStartAt = -1f;
            var session = SpadesNetworkSession.GetOrCreate();
            var rules = session.PendingRules ?? BackyardLegendsSession.GetOrCreateRuntimeInstance().SelectedRule;
            ruleEngine = new SpadesRuleEngine();
            hostController = new SpadesMatchController(rules, ruleEngine, new Dictionary<SeatId, IAiAgent>());
            foreach (var seat in humanOwnedSeats)
            {
                hostController.State.SeatNames[seat] = displayNameBySeat.TryGetValue(seat, out var name)
                    ? name
                    : $"Player {(int)seat + 1}";
            }

            hostController.EventRaised += HandleHostMatchEvent;
            matchStarted = true;
            readyForMatch.Clear();

            if (!string.IsNullOrEmpty(session.TableId))
            {
                _ = TableSessionService.MarkInPlayAsync(session.TableId);
            }

            session.MarkMatchLive();
            MatchBound?.Invoke();
            LobbyStateChanged?.Invoke();

            var readyPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.TableReady,
                Message = "All ready. Dealing…",
                PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
            };
            SendEventClientRpc(readyPayload);

            hostController.StartMatch();
            AdvanceAiUntilHumanOrIdle();
            PersistAuthoritySnapshot();
            session.SetStatus("Match live");
        }

        private void BroadcastLobbyRoster()
        {
            if (!IsServer || matchStarted)
            {
                return;
            }

            var parts = new List<string>();
            foreach (var seat in new[] { SeatId.Bottom, SeatId.Top, SeatId.Left, SeatId.Right })
            {
                var name = displayNameBySeat.TryGetValue(seat, out var n) ? n : "—";
                var ready = readyForMatch.Contains(seat) ? "R" : "-";
                var team = seat == SeatId.Bottom || seat == SeatId.Top ? "Home" : "Away";
                parts.Add($"{seat}:{name}:{ready}:{team}");
            }

            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.LobbyRoster,
                Message = string.Join("|", parts)
            };
            SendEventClientRpc(payload);
            LobbyStateChanged?.Invoke();
        }

        private void BeginGracePeriod(SeatId seat, string reason)
        {
            if (hostController == null || hostController.State.Phase == MatchPhase.MatchEnded)
            {
                return;
            }

            graceEndsAtBySeat[seat] = Time.time + ReconnectGraceSeconds;
            gracePendingSeats.Add(seat);
            var originalName = displayNameBySeat.TryGetValue(seat, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : seat.ToString();

            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId) && uidBySeat.TryGetValue(seat, out var awayUid))
            {
                _ = TableSessionService.UpsertSeatAsync(session.TableId, seat, awayUid, originalName, "grace");
                _ = TableSessionService.SetStatusAsync(session.TableId, "paused");
            }

            var awayPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.PlayerAway,
                Seat = (byte)seat,
                Message = $"{originalName} disconnected — 90s to reconnect ({reason})",
                PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
            };
            SendEventClientRpc(awayPayload);
            BroadcastTableEvent(SpadesNetworkEventKind.TablePaused, $"Waiting for {originalName} (90s)");
            session.SetStatus($"Paused — waiting for {originalName}");
        }

        private void ActivateAiSitInAfterGrace(SeatId seat)
        {
            if (hostController == null || hostController.State.Phase == MatchPhase.MatchEnded)
            {
                return;
            }

            if (!aiSitInSeats.Contains(seat))
            {
                hostController.SetAiSitIn(seat, new SimpleAiAgent());
                aiSitInSeats.Add(seat);
            }

            var originalName = displayNameBySeat.TryGetValue(seat, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : seat.ToString();
            hostController.State.SeatNames[seat] = $"{originalName} (AI)";

            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId) && uidBySeat.TryGetValue(seat, out var awayUid))
            {
                _ = TableSessionService.UpsertSeatAsync(session.TableId, seat, awayUid, originalName, "ai");
            }

            var awayPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.PlayerAway,
                Seat = (byte)seat,
                Message = $"{originalName} away — AI sitting in",
                PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
            };
            SendEventClientRpc(awayPayload);
        }

        private void BeginAiSitIn(SeatId seat, string reason)
        {
            // Immediate AI path retained for restore / host-failover edge cases.
            BeginGracePeriod(seat, reason);
            graceEndsAtBySeat[seat] = Time.time;
            gracePendingSeats.Add(seat);
        }

        private void BroadcastTableEvent(SpadesNetworkEventKind kind, string message)
        {
            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)kind,
                Message = message ?? string.Empty,
                PublicState = hostController != null
                    ? SpadesNetworkPublicState.FromMatchState(hostController.State)
                    : null
            };
            SendEventClientRpc(payload);
        }

        private void SeedSeatsFromPendingRoster(SpadesNetworkSession session)
        {
            var roster = session.ConsumePendingSeatRoster();
            if (roster == null)
            {
                return;
            }

            foreach (var pair in roster)
            {
                var seat = pair.Key;
                var uid = pair.Value.Uid;
                var displayName = pair.Value.DisplayName;
                if (string.IsNullOrWhiteSpace(uid))
                {
                    continue;
                }

                humanOwnedSeats.Add(seat);
                uidBySeat[seat] = uid;
                seatByUid[uid] = seat;
                displayNameBySeat[seat] = string.IsNullOrWhiteSpace(displayName) ? seat.ToString() : displayName;
                if (hostController != null)
                {
                    hostController.State.SeatNames[seat] = displayNameBySeat[seat];
                }

                // Local host client mapping if this is our uid.
                if (string.Equals(uid, session.LocalPlayerId, StringComparison.Ordinal) &&
                    NetworkManager != null)
                {
                    var localId = NetworkManager.LocalClientId;
                    seatByClient[localId] = seat;
                    clientBySeat[seat] = localId;
                    connectedHumanSeats.Add(seat);
                    uidByClient[localId] = uid;
                }
            }
        }

        private SeatId? PickNextSeat()
        {
            foreach (var seat in new[] { SeatId.Bottom, SeatId.Top, SeatId.Left, SeatId.Right })
            {
                if (!clientBySeat.ContainsKey(seat) && !humanOwnedSeats.Contains(seat))
                {
                    return seat;
                }
            }

            return null;
        }

        private SeatId? PickAiFillSeat()
        {
            foreach (var seat in new[] { SeatId.Top, SeatId.Left, SeatId.Right, SeatId.Bottom })
            {
                if (humanOwnedSeats.Contains(seat) || clientBySeat.ContainsKey(seat))
                {
                    continue;
                }

                // Human-owned sit-in seats need uid reclaim, not open takeover.
                if (aiSitInSeats.Contains(seat))
                {
                    continue;
                }

                if (hostController != null && !hostController.IsAiSeat(seat))
                {
                    continue;
                }

                return seat;
            }

            return null;
        }

        private void TakeOverAiFillSeat(ulong clientId, SeatId seat, string uid, string displayName)
        {
            if (hostController != null)
            {
                hostController.ClearAiSitIn(seat);
                hostController.State.SeatNames[seat] = displayName;
            }

            aiSitInSeats.Remove(seat);
            graceEndsAtBySeat.Remove(seat);
            BindClientToSeat(clientId, seat, uid, displayName, sendCatchUp: true);
            Debug.Log($"Client {clientId} took over AI-fill seat {seat} as '{displayName}'");

            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.PlayerReturned,
                Seat = (byte)seat,
                Message = $"{displayName} joined",
                PublicState = hostController != null
                    ? SpadesNetworkPublicState.FromMatchState(hostController.State)
                    : null
            };
            SendEventClientRpc(payload);
            SpadesNetworkSession.GetOrCreate().SetStatus("Match live");
        }

        private void TryStartMatchWithAiFill()
        {
            if (matchStarted)
            {
                return;
            }

            // Wait until connected clients have registered identities.
            if (pendingRegistration.Count > 0)
            {
                autoStartAt = Time.time + 0.75f;
                return;
            }

            // Don't deal while NGO has connections that are not seated yet.
            if (NetworkManager != null)
            {
                var connected = NetworkManager.ConnectedClientsIds.Count;
                if (connected > connectedHumanSeats.Count)
                {
                    autoStartAt = Time.time + 1f;
                    SpadesNetworkSession.GetOrCreate().SetStatus(
                        $"Waiting for player registration ({connectedHumanSeats.Count}/{connected})…");
                    return;
                }
            }

            if (connectedHumanSeats.Count == 0)
            {
                autoStartAt = Time.time + 1.25f;
                return;
            }

            // Any client currently mapped must count as human before AI fill.
            foreach (var pair in seatByClient)
            {
                humanOwnedSeats.Add(pair.Value);
                connectedHumanSeats.Add(pair.Value);
            }

            autoStartAt = -1f;
            var session = SpadesNetworkSession.GetOrCreate();
            var rules = session.PendingRules ?? BackyardLegendsSession.GetOrCreateRuntimeInstance().SelectedRule;
            ruleEngine = new SpadesRuleEngine();

            var restore = session.PendingRestoreState;
            if (restore != null)
            {
                SeedSeatsFromPendingRoster(session);
            }

            var aiAgents = new Dictionary<SeatId, IAiAgent>();
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                // On restore, unconnected human seats still get AI sit-in until reclaim.
                if (!humanOwnedSeats.Contains(seat) || (restore != null && !connectedHumanSeats.Contains(seat)))
                {
                    aiAgents[seat] = new SimpleAiAgent();
                    if (humanOwnedSeats.Contains(seat))
                    {
                        aiSitInSeats.Add(seat);
                    }
                }
            }

            Debug.Log(
                $"Starting match humans=[{string.Join(",", humanOwnedSeats)}] " +
                $"ai=[{string.Join(",", aiAgents.Keys)}] connected={connectedHumanSeats.Count}");

            hostController = new SpadesMatchController(rules, ruleEngine, aiAgents);
            foreach (var seat in humanOwnedSeats)
            {
                hostController.State.SeatNames[seat] = displayNameBySeat.TryGetValue(seat, out var name)
                    ? name
                    : $"Player {(int)seat + 1}";
            }

            foreach (var seat in aiAgents.Keys)
            {
                if (!humanOwnedSeats.Contains(seat))
                {
                    hostController.State.SeatNames[seat] = $"{seat} AI";
                }
                else if (displayNameBySeat.TryGetValue(seat, out var nm))
                {
                    hostController.State.SeatNames[seat] = $"{nm} (AI)";
                }
            }

            hostController.EventRaised += HandleHostMatchEvent;
            matchStarted = true;

            if (restore != null)
            {
                restore = session.ConsumePendingRestoreState(out var restoreSeq);
                actionSeq = restoreSeq;
                hostController.RestoreState(restore);
                session.MarkMatchLive();
                MatchBound?.Invoke();
                var catchUp = new SpadesNetworkEventPayload
                {
                    Kind = (byte)SpadesNetworkEventKind.CatchUpState,
                    Message = "Host restored — match continues",
                    PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
                };
                SendEventClientRpc(catchUp);
                PushPrivateHands();
                AdvanceAiUntilHumanOrIdle();
                PersistAuthoritySnapshot();
                session.SetStatus("Match live (restored)");
                return;
            }

            session.MarkMatchLive();
            MatchBound?.Invoke();

            var readyPayload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.TableReady,
                Message = "Table locked. Dealing…",
                PublicState = SpadesNetworkPublicState.FromMatchState(hostController.State)
            };
            SendEventClientRpc(readyPayload);

            hostController.StartMatch();
            AdvanceAiUntilHumanOrIdle();
            PersistAuthoritySnapshot();
            session.SetStatus("Match live");
        }

        private void HandleHostMatchEvent(SpadesMatchEvent matchEvent)
        {
            actionSeq++;
            BroadcastMatchEvent(matchEvent);
            PushPrivateHands();
            PersistAuthoritySnapshot();
            if (IsHost)
            {
                var localPayload = BuildPayload(matchEvent, includeState: true);
                ApplyLocalEvent(localPayload);
            }

            if (matchEvent is MatchEndedEvent ended)
            {
                var session = SpadesNetworkSession.GetOrCreate();
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    _ = TableSessionService.MarkCompletedAsync(session.TableId, ended.WinningTeam);
                }
            }
        }

        private void PersistAuthoritySnapshot()
        {
            if (!IsServer || hostController == null)
            {
                return;
            }

            var session = SpadesNetworkSession.GetOrCreate();
            if (string.IsNullOrEmpty(session.TableId) || !TableSessionService.IsAvailable)
            {
                return;
            }

            var state = hostController.CloneState();
            _ = TableSessionService.WriteAuthoritySnapshotAsync(
                session.TableId,
                state,
                actionSeq,
                uidBySeat,
                session.SessionKey);
        }

        private IEnumerator HostHeartbeatRoutine()
        {
            var wait = new WaitForSecondsRealtime(TableSessionService.HostHeartbeatSeconds);
            while (IsServer)
            {
                var session = SpadesNetworkSession.GetOrCreate();
                if (!string.IsNullOrEmpty(session.TableId) && TableSessionService.IsAvailable)
                {
                    _ = TableSessionService.HeartbeatAsync(session.TableId, session.LocalPlayerId);
                }

                yield return wait;
            }
        }

        private IEnumerator ResolveTableSessionFromJoinCodeRoutine()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId) || string.IsNullOrEmpty(session.JoinCode))
            {
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    SpadesHostFailover.GetOrCreate().BeginWatchingTable(session.TableId);
                }

                yield break;
            }

            if (!TableSessionService.IsAvailable)
            {
                var init = FirebaseBootstrap.EnsureInitializedAsync();
                while (!init.IsCompleted)
                {
                    yield return null;
                }
            }

            if (!TableSessionService.IsAvailable)
            {
                yield break;
            }

            // Give host a moment to write the table doc.
            yield return new WaitForSecondsRealtime(1.5f);
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (!string.IsNullOrEmpty(session.TableId))
                {
                    yield break;
                }

                var task = TableSessionService.FindByJoinCodeAsync(session.JoinCode);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result != null)
                {
                    var table = task.Result;
                    session.SetTableSession(table.TableId, table.SessionKey, table.HostUid);
                    SpadesHostFailover.GetOrCreate().BeginWatchingTable(table.TableId);
                    Debug.Log($"Resolved Firestore table {table.TableId} from join code {session.JoinCode}");
                    yield break;
                }

                yield return new WaitForSecondsRealtime(1.25f);
            }
        }

        public void SyncTableSessionToClients()
        {
            if (!IsServer)
            {
                return;
            }

            var session = SpadesNetworkSession.GetOrCreate();
            if (string.IsNullOrEmpty(session.TableId))
            {
                return;
            }

            BroadcastTableSessionClientRpc(
                session.TableId,
                session.SessionKey,
                string.IsNullOrEmpty(session.LastKnownHostUid) ? session.LocalPlayerId : session.LastKnownHostUid);

            if (heartbeatRoutine == null)
            {
                heartbeatRoutine = StartCoroutine(HostHeartbeatRoutine());
            }
        }

        [ClientRpc]
        private void BroadcastTableSessionClientRpc(
            string tableId,
            string sessionKey,
            string hostUid,
            ClientRpcParams rpcParams = default)
        {
            var session = SpadesNetworkSession.GetOrCreate();
            session.SetTableSession(tableId, sessionKey, hostUid);
            if (!string.IsNullOrEmpty(tableId))
            {
                SpadesHostFailover.GetOrCreate().BeginWatchingTable(tableId);
            }
        }

        private void BroadcastMatchEvent(SpadesMatchEvent matchEvent)
        {
            var payload = BuildPayload(matchEvent, includeState: true);
            SendEventClientRpc(payload);
        }

        private SpadesNetworkEventPayload BuildPayload(SpadesMatchEvent matchEvent, bool includeState)
        {
            var payload = new SpadesNetworkEventPayload
            {
                Message = string.Empty,
                PublicState = includeState
                    ? SpadesNetworkPublicState.FromMatchState(matchEvent.Snapshot)
                    : null
            };

            switch (matchEvent)
            {
                case MatchStartedEvent:
                    payload.Kind = (byte)SpadesNetworkEventKind.MatchStarted;
                    break;
                case RoundStartedEvent:
                    payload.Kind = (byte)SpadesNetworkEventKind.RoundStarted;
                    break;
                case BidSubmittedEvent bid:
                    payload.Kind = (byte)SpadesNetworkEventKind.BidSubmitted;
                    payload.Seat = (byte)bid.Seat;
                    payload.Bid = bid.Bid;
                    break;
                case CardPlayedEvent played:
                    payload.Kind = (byte)SpadesNetworkEventKind.CardPlayed;
                    payload.Seat = (byte)played.Seat;
                    payload.Card = SpadesNetworkCard.FromCard(played.Card);
                    break;
                case TrickResolvedEvent trick:
                    payload.Kind = (byte)SpadesNetworkEventKind.TrickResolved;
                    payload.Seat = (byte)trick.Winner;
                    payload.CompletedTrick = trick.CompletedTrick.Select(play => new SpadesNetworkTrickPlay
                    {
                        Seat = (byte)play.Seat,
                        Card = SpadesNetworkCard.FromCard(play.Card)
                    }).ToArray();
                    break;
                case RoundScoredEvent scored:
                    payload.Kind = (byte)SpadesNetworkEventKind.RoundScored;
                    payload.Message = scored.RoundSummary ?? string.Empty;
                    break;
                case RemainingBooksClaimedEvent claimed:
                    payload.Kind = (byte)SpadesNetworkEventKind.RemainingBooksClaimed;
                    payload.Team = (byte)claimed.Team;
                    payload.ClaimedBooks = claimed.ClaimedBooks;
                    break;
                case MatchEndedEvent ended:
                    payload.Kind = (byte)SpadesNetworkEventKind.MatchEnded;
                    payload.WinningTeam = (byte)ended.WinningTeam;
                    break;
                case MatchForfeitedEvent forfeited:
                    payload.Kind = (byte)SpadesNetworkEventKind.MatchForfeited;
                    payload.Team = (byte)forfeited.ForfeitingTeam;
                    payload.WinningTeam = (byte)forfeited.WinningTeam;
                    break;
                case SetBookReachedEvent setBook:
                    payload.Kind = (byte)SpadesNetworkEventKind.SetBookReached;
                    payload.Team = (byte)setBook.Team;
                    break;
                default:
                    payload.Kind = (byte)SpadesNetworkEventKind.MatchStarted;
                    break;
            }

            return payload;
        }

        private void PushPrivateHands()
        {
            if (hostController?.State?.RoundState?.HandsBySeat == null)
            {
                return;
            }

            foreach (var pair in seatByClient)
            {
                PushPrivateHandToClient(pair.Key, pair.Value);
            }
        }

        private void PushPrivateHandToClient(ulong clientId, SeatId seat)
        {
            if (hostController?.State?.RoundState?.HandsBySeat == null)
            {
                return;
            }

            if (!hostController.State.RoundState.HandsBySeat.TryGetValue(seat, out var hand))
            {
                return;
            }

            var cards = hand.Select(SpadesNetworkCard.FromCard).ToArray();
            privateHandsByClient[clientId] = hand.ToList();
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
            SendPrivateHandClientRpc(cards, target);

            if (clientId == NetworkManager.LocalClientId)
            {
                localPrivateHand = hand.ToList();
                PrivateHandUpdated?.Invoke();
            }
        }

        [ClientRpc]
        private void SendPrivateHandClientRpc(SpadesNetworkCard[] cards, ClientRpcParams rpcParams = default)
        {
            localPrivateHand = cards != null
                ? cards.Select(card => card.ToCard()).ToList()
                : new List<Card>();
            PrivateHandUpdated?.Invoke();
        }

        [ClientRpc]
        private void SendEventClientRpc(SpadesNetworkEventPayload payload, ClientRpcParams rpcParams = default)
        {
            if (IsHost &&
                payload.Kind != (byte)SpadesNetworkEventKind.SeatAssigned &&
                payload.Kind != (byte)SpadesNetworkEventKind.CatchUpState)
            {
                if (payload.Kind != (byte)SpadesNetworkEventKind.TableReady &&
                    payload.Kind != (byte)SpadesNetworkEventKind.ActionRejected &&
                    payload.Kind != (byte)SpadesNetworkEventKind.PlayerAway &&
                    payload.Kind != (byte)SpadesNetworkEventKind.PlayerReturned)
                {
                    return;
                }
            }

            ApplyLocalEvent(payload);
        }

        private void ApplyLocalEvent(SpadesNetworkEventPayload payload)
        {
            if (payload.Kind == (byte)SpadesNetworkEventKind.SeatAssigned)
            {
                ApplyLocalSeat((SeatId)payload.Seat, payload.Message);
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.MatchEnded ||
                payload.Kind == (byte)SpadesNetworkEventKind.MatchForfeited)
            {
                SpadesNetworkSession.GetOrCreate().DisableAutoReconnect();
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.TableReady)
            {
                SpadesNetworkSession.GetOrCreate().MarkMatchLive();
                LobbyStateChanged?.Invoke();
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.LobbyRoster ||
                payload.Kind == (byte)SpadesNetworkEventKind.LobbyReadyChanged)
            {
                LobbyStateChanged?.Invoke();
            }

            NetworkEventReceived?.Invoke(payload);
        }

        private void ApplyLocalSeat(SeatId seat, string displayName)
        {
            SpadesNetworkSession.GetOrCreate().AssignLocalSeat(seat);
            LocalSeatAssigned?.Invoke(seat);
            if (!string.IsNullOrEmpty(displayName))
            {
                SpadesNetworkSession.GetOrCreate().SetStatus($"Seated as {displayName} ({seat})");
            }
        }

        private bool TryGetCallerSeat(ulong clientId, out SeatId seat, out string error)
        {
            error = string.Empty;
            if (!seatByClient.TryGetValue(clientId, out seat))
            {
                error = "You are not seated at this table.";
                return false;
            }

            return true;
        }

        private void Reject(ulong clientId, string error)
        {
            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.ActionRejected,
                Message = error ?? "Action rejected."
            };
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
            SendEventClientRpc(payload, target);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitBidServerRpc(int bid, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var seatError = string.Empty;
            if (!EnsureMatchLive(clientId) || !TryGetCallerSeat(clientId, out var seat, out seatError))
            {
                if (!string.IsNullOrEmpty(seatError))
                {
                    Reject(clientId, seatError);
                }

                return;
            }

            if (!hostController.TrySubmitBid(seat, bid, out var error))
            {
                Reject(clientId, error);
                return;
            }

            AdvanceAiUntilHumanOrIdle();
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayCardServerRpc(byte suit, byte rank, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var seatError = string.Empty;
            if (!EnsureMatchLive(clientId) || !TryGetCallerSeat(clientId, out var seat, out seatError))
            {
                if (!string.IsNullOrEmpty(seatError))
                {
                    Reject(clientId, seatError);
                }

                return;
            }

            var card = new Card((Suit)suit, rank);
            if (!hostController.TryPlayCard(seat, card, out var error))
            {
                Reject(clientId, error);
                return;
            }

            AdvanceAiUntilHumanOrIdle();
        }

        [ServerRpc(RequireOwnership = false)]
        public void ClaimRemainingBooksServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var seatError = string.Empty;
            if (!EnsureMatchLive(clientId) || !TryGetCallerSeat(clientId, out var seat, out seatError))
            {
                if (!string.IsNullOrEmpty(seatError))
                {
                    Reject(clientId, seatError);
                }

                return;
            }

            if (!hostController.TryClaimRemainingBooks(seat.ToTeam(), out var error))
            {
                Reject(clientId, error);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ForfeitMatchServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var seatError = string.Empty;
            if (!EnsureMatchLive(clientId) || !TryGetCallerSeat(clientId, out var seat, out seatError))
            {
                if (!string.IsNullOrEmpty(seatError))
                {
                    Reject(clientId, seatError);
                }

                return;
            }

            if (!hostController.TryForfeitMatch(seat.ToTeam(), out var error))
            {
                Reject(clientId, error);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReadyForNextHandServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var seatError = string.Empty;
            if (!EnsureMatchLive(clientId) || !TryGetCallerSeat(clientId, out var seat, out seatError))
            {
                if (!string.IsNullOrEmpty(seatError))
                {
                    Reject(clientId, seatError);
                }

                return;
            }

            if (hostController.State.Phase != MatchPhase.RoundSummary)
            {
                Reject(clientId, "Next hand is not available yet.");
                return;
            }

            readyForNextHand.Add(seat);
            foreach (var human in connectedHumanSeats)
            {
                if (!readyForNextHand.Contains(human))
                {
                    return;
                }
            }

            readyForNextHand.Clear();
            hostController.StartNextRound();
            AdvanceAiUntilHumanOrIdle();
        }

        private bool EnsureMatchLive(ulong clientId)
        {
            if (matchStarted && hostController != null)
            {
                return true;
            }

            Reject(clientId, "Match has not started yet.");
            return false;
        }

        private void AdvanceAiUntilHumanOrIdle()
        {
            if (hostController == null || gracePendingSeats.Count > 0)
            {
                return;
            }

            var guard = 0;
            while (hostController.NeedsAiTurn && guard++ < 64)
            {
                hostController.AdvanceAiTurn();
            }
        }

        public bool TryGetSeatForClient(ulong clientId, out SeatId seat)
        {
            return seatByClient.TryGetValue(clientId, out seat);
        }

        public MatchState BuildClientPresentationState(SeatId localSeat)
        {
            if (IsServer && hostController != null)
            {
                return hostController.State;
            }

            return null;
        }
    }
}
