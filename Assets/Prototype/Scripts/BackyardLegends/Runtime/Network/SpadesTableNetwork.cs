using System;
using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using Unity.Netcode;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesTableNetwork : NetworkBehaviour
    {
        public static SpadesTableNetwork Instance { get; private set; }

        private readonly Dictionary<ulong, SeatId> seatByClient = new();
        private readonly Dictionary<SeatId, ulong> clientBySeat = new();
        private readonly HashSet<SeatId> humanSeats = new();
        private readonly HashSet<SeatId> readyForNextHand = new();
        private readonly Dictionary<ulong, List<Card>> privateHandsByClient = new();

        private SpadesMatchController hostController;
        private SpadesRuleEngine ruleEngine;
        private bool matchStarted;
        private float autoStartAt = -1f;
        private List<Card> localPrivateHand = new();

        public SpadesMatchController HostController => hostController;
        public bool MatchStarted => matchStarted;
        public IReadOnlyList<Card> LocalPrivateHand => localPrivateHand;

        public event Action<SpadesNetworkEventPayload> NetworkEventReceived;
        public event Action<SeatId> LocalSeatAssigned;
        public event Action MatchBound;
        public event Action PrivateHandUpdated;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                BindExistingClients();
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                autoStartAt = Time.time + 2.5f;
                SpadesNetworkSession.GetOrCreate().SetStatus("Waiting for players (AI fills empty seats)…");
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

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsServer || matchStarted || autoStartAt < 0f)
            {
                return;
            }

            if (Time.time >= autoStartAt)
            {
                TryStartMatchWithAiFill();
            }
        }

        private void BindExistingClients()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                AssignSeatToClient(clientId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (matchStarted)
            {
                return;
            }

            AssignSeatToClient(clientId);
            autoStartAt = Time.time + 1.25f;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!seatByClient.TryGetValue(clientId, out var seat))
            {
                return;
            }

            seatByClient.Remove(clientId);
            clientBySeat.Remove(seat);
            humanSeats.Remove(seat);
            privateHandsByClient.Remove(clientId);
            readyForNextHand.Remove(seat);
        }

        private void AssignSeatToClient(ulong clientId)
        {
            if (seatByClient.ContainsKey(clientId))
            {
                return;
            }

            var seat = PickNextSeat();
            if (!seat.HasValue)
            {
                Debug.LogWarning($"No seats left for client {clientId}");
                return;
            }

            seatByClient[clientId] = seat.Value;
            clientBySeat[seat.Value] = clientId;
            humanSeats.Add(seat.Value);

            var displayName = clientId == NetworkManager.LocalClientId
                ? ResolveLocalDisplayName()
                : $"Player {(int)seat.Value + 1}";

            var payload = new SpadesNetworkEventPayload
            {
                Kind = (byte)SpadesNetworkEventKind.SeatAssigned,
                Seat = (byte)seat.Value,
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
                ApplyLocalSeat(seat.Value, displayName);
            }
        }

        private SeatId? PickNextSeat()
        {
            foreach (var seat in new[] { SeatId.Bottom, SeatId.Top, SeatId.Left, SeatId.Right })
            {
                if (!clientBySeat.ContainsKey(seat))
                {
                    return seat;
                }
            }

            return null;
        }

        private static string ResolveLocalDisplayName()
        {
            var session = BackyardLegendsSession.Instance;
            if (session?.CurrentUser != null && session.CurrentUser.IsSignedIn &&
                !string.IsNullOrWhiteSpace(session.CurrentUser.DisplayName))
            {
                return session.CurrentUser.DisplayName;
            }

            return "You";
        }

        private void TryStartMatchWithAiFill()
        {
            if (matchStarted)
            {
                return;
            }

            autoStartAt = -1f;
            var session = SpadesNetworkSession.GetOrCreate();
            var rules = session.PendingRules ?? BackyardLegendsSession.GetOrCreateRuntimeInstance().SelectedRule;
            ruleEngine = new SpadesRuleEngine();

            var aiAgents = new Dictionary<SeatId, IAiAgent>();
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                if (!humanSeats.Contains(seat))
                {
                    aiAgents[seat] = new SimpleAiAgent();
                }
            }

            hostController = new SpadesMatchController(rules, ruleEngine, aiAgents);
            foreach (var pair in seatByClient)
            {
                var name = pair.Key == NetworkManager.LocalClientId
                    ? ResolveLocalDisplayName()
                    : $"Player {(int)pair.Value + 1}";
                hostController.State.SeatNames[pair.Value] = name;
            }

            foreach (var seat in aiAgents.Keys)
            {
                hostController.State.SeatNames[seat] = $"{seat} AI";
            }

            hostController.EventRaised += HandleHostMatchEvent;
            matchStarted = true;
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
            session.SetStatus("Match live");
        }

        private void HandleHostMatchEvent(SpadesMatchEvent matchEvent)
        {
            BroadcastMatchEvent(matchEvent);
            PushPrivateHands();
            if (IsHost)
            {
                // Host also drives local presentation through the same event path.
                var localPayload = BuildPayload(matchEvent, includeState: true);
                ApplyLocalEvent(localPayload);
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
                var seat = pair.Value;
                if (!hostController.State.RoundState.HandsBySeat.TryGetValue(seat, out var hand))
                {
                    continue;
                }

                var cards = hand.Select(SpadesNetworkCard.FromCard).ToArray();
                privateHandsByClient[pair.Key] = hand.ToList();
                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { pair.Key }
                    }
                };
                SendPrivateHandClientRpc(cards, target);

                if (pair.Key == NetworkManager.LocalClientId)
                {
                    localPrivateHand = hand.ToList();
                }
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
            if (IsHost && payload.Kind != (byte)SpadesNetworkEventKind.SeatAssigned)
            {
                // Host already applied live controller events locally in HandleHostMatchEvent.
                if (payload.Kind != (byte)SpadesNetworkEventKind.TableReady &&
                    payload.Kind != (byte)SpadesNetworkEventKind.ActionRejected)
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
            foreach (var human in humanSeats)
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
            if (hostController == null)
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
