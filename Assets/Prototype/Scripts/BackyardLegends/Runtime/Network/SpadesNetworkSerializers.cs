using System;
using System.Collections.Generic;
using BackyardLegends.Core;
using Unity.Netcode;

namespace BackyardLegends.Runtime.Network
{
    public enum SpadesNetworkEventKind : byte
    {
        MatchStarted = 1,
        RoundStarted = 2,
        BidSubmitted = 3,
        CardPlayed = 4,
        TrickResolved = 5,
        RoundScored = 6,
        RemainingBooksClaimed = 7,
        MatchEnded = 8,
        MatchForfeited = 9,
        SetBookReached = 10,
        ActionRejected = 11,
        SeatAssigned = 12,
        TableReady = 13
    }

    public struct SpadesNetworkCard : INetworkSerializable
    {
        public byte Suit;
        public byte Rank;

        public static SpadesNetworkCard FromCard(Card card)
        {
            return new SpadesNetworkCard
            {
                Suit = (byte)card.Suit,
                Rank = (byte)card.Rank
            };
        }

        public Card ToCard()
        {
            return new Card((Suit)Suit, Rank);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Suit);
            serializer.SerializeValue(ref Rank);
        }
    }

    public struct SpadesNetworkScore : INetworkSerializable
    {
        public byte Team;
        public int Score;
        public int Bags;
        public int ContractBid;
        public int TricksWon;
        public int RoundDelta;
        public int NilDelta;
        public int RenegeDelta;
        public int BagsEarned;
        public int BagPenaltyDelta;

        public static SpadesNetworkScore FromSnapshot(ScoreSnapshot snapshot)
        {
            return new SpadesNetworkScore
            {
                Team = (byte)snapshot.Team,
                Score = snapshot.Score,
                Bags = snapshot.Bags,
                ContractBid = snapshot.ContractBid,
                TricksWon = snapshot.TricksWon,
                RoundDelta = snapshot.RoundDelta,
                NilDelta = snapshot.NilDelta,
                RenegeDelta = snapshot.RenegeDelta,
                BagsEarned = snapshot.BagsEarned,
                BagPenaltyDelta = snapshot.BagPenaltyDelta
            };
        }

        public ScoreSnapshot ToSnapshot()
        {
            return new ScoreSnapshot
            {
                Team = (TeamId)Team,
                Score = Score,
                Bags = Bags,
                ContractBid = ContractBid,
                TricksWon = TricksWon,
                RoundDelta = RoundDelta,
                NilDelta = NilDelta,
                RenegeDelta = RenegeDelta,
                BagsEarned = BagsEarned,
                BagPenaltyDelta = BagPenaltyDelta
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref Bags);
            serializer.SerializeValue(ref ContractBid);
            serializer.SerializeValue(ref TricksWon);
            serializer.SerializeValue(ref RoundDelta);
            serializer.SerializeValue(ref NilDelta);
            serializer.SerializeValue(ref RenegeDelta);
            serializer.SerializeValue(ref BagsEarned);
            serializer.SerializeValue(ref BagPenaltyDelta);
        }
    }

    public struct SpadesNetworkTrickPlay : INetworkSerializable
    {
        public byte Seat;
        public SpadesNetworkCard Card;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref Card);
        }
    }

    /// <summary>
    /// Public match snapshot for clients. Opponent hands are counts only.
    /// </summary>
    public class SpadesNetworkPublicState : INetworkSerializable
    {
        public byte Phase;
        public int TargetScore;
        public byte HasWinningTeam;
        public byte WinningTeam;
        public string StatusMessage;
        public int RoundNumber;
        public byte Dealer;
        public byte CurrentBidder;
        public byte TrickLeader;
        public byte CurrentTurn;
        public byte HasLeadSuit;
        public byte LeadSuit;
        public byte SpadesBroken;
        public string[] SeatNames = Array.Empty<string>();
        public int?[] Bids = Array.Empty<int?>();
        public int[] TricksWon = Array.Empty<int>();
        public int[] HandCounts = Array.Empty<int>();
        public SpadesNetworkScore[] Scores = Array.Empty<SpadesNetworkScore>();
        public SpadesNetworkTrickPlay[] CurrentPlays = Array.Empty<SpadesNetworkTrickPlay>();
        public byte[] RenegeSeats = Array.Empty<byte>();
        public string RuleDisplayName = "Classic";
        public bool SpadesMustBeBroken = true;
        public bool AllowSpadesAnytime;
        public bool FollowSuitRequired = true;
        public bool RenegePenaltyEnabled;

        public static SpadesNetworkPublicState FromMatchState(MatchState state, SeatId? privateHandSeat = null, IReadOnlyList<Card> privateHand = null)
        {
            var payload = new SpadesNetworkPublicState
            {
                Phase = (byte)state.Phase,
                TargetScore = state.TargetScore,
                HasWinningTeam = (byte)(state.WinningTeam.HasValue ? 1 : 0),
                WinningTeam = (byte)(state.WinningTeam ?? TeamId.Home),
                StatusMessage = state.RoundState?.LastStatusMessage ?? string.Empty,
                SeatNames = new string[4],
                Bids = new int?[4],
                TricksWon = new int[4],
                HandCounts = new int[4],
                Scores = new SpadesNetworkScore[2]
            };

            if (state.RuleSet != null)
            {
                payload.RuleDisplayName = state.RuleSet.DisplayName ?? "Classic";
                payload.SpadesMustBeBroken = state.RuleSet.SpadesMustBeBroken;
                payload.AllowSpadesAnytime = state.RuleSet.AllowSpadesAnytime;
                payload.FollowSuitRequired = state.RuleSet.FollowSuitRequired;
                payload.RenegePenaltyEnabled = state.RuleSet.RenegePenaltyEnabled;
            }

            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                payload.SeatNames[i] = state.SeatNames != null && state.SeatNames.TryGetValue(seat, out var name)
                    ? name
                    : seat.DisplayName();
            }

            if (state.Scores != null)
            {
                payload.Scores[0] = state.Scores.TryGetValue(TeamId.Home, out var home)
                    ? SpadesNetworkScore.FromSnapshot(home)
                    : new SpadesNetworkScore { Team = (byte)TeamId.Home };
                payload.Scores[1] = state.Scores.TryGetValue(TeamId.Away, out var away)
                    ? SpadesNetworkScore.FromSnapshot(away)
                    : new SpadesNetworkScore { Team = (byte)TeamId.Away };
            }

            if (state.RoundState == null)
            {
                return payload;
            }

            payload.RoundNumber = state.RoundState.RoundNumber;
            payload.Dealer = (byte)state.RoundState.Dealer;
            payload.CurrentBidder = (byte)state.RoundState.BidState.CurrentBidder;
            payload.TrickLeader = (byte)state.RoundState.TrickState.Leader;
            payload.CurrentTurn = (byte)state.RoundState.TrickState.CurrentTurn;
            payload.HasLeadSuit = (byte)(state.RoundState.TrickState.LeadSuit.HasValue ? 1 : 0);
            payload.LeadSuit = (byte)(state.RoundState.TrickState.LeadSuit ?? Suit.Clubs);
            payload.SpadesBroken = (byte)(state.RoundState.TrickState.SpadesBroken ? 1 : 0);

            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                if (state.RoundState.BidState.BidsBySeat.TryGetValue(seat, out var bid))
                {
                    payload.Bids[i] = bid;
                }

                if (state.RoundState.TricksWonBySeat.TryGetValue(seat, out var tricks))
                {
                    payload.TricksWon[i] = tricks;
                }

                if (state.RoundState.HandsBySeat.TryGetValue(seat, out var hand) && hand != null)
                {
                    payload.HandCounts[i] = hand.Count;
                }
            }

            if (privateHandSeat.HasValue && privateHand != null)
            {
                payload.HandCounts[(int)privateHandSeat.Value] = privateHand.Count;
            }

            var plays = state.RoundState.TrickState.Plays;
            payload.CurrentPlays = new SpadesNetworkTrickPlay[plays.Count];
            for (var i = 0; i < plays.Count; i++)
            {
                payload.CurrentPlays[i] = new SpadesNetworkTrickPlay
                {
                    Seat = (byte)plays[i].Seat,
                    Card = SpadesNetworkCard.FromCard(plays[i].Card)
                };
            }

            payload.RenegeSeats = new byte[state.RoundState.RenegeSeats.Count];
            for (var i = 0; i < state.RoundState.RenegeSeats.Count; i++)
            {
                payload.RenegeSeats[i] = (byte)state.RoundState.RenegeSeats[i];
            }

            return payload;
        }

        public MatchState ToMatchState(SeatId localSeat, IReadOnlyList<Card> localHand)
        {
            var state = new MatchState
            {
                Phase = (MatchPhase)Phase,
                TargetScore = TargetScore,
                WinningTeam = HasWinningTeam == 1 ? (TeamId?)(TeamId)WinningTeam : null,
                RuleSet = new RuleSetDefinition
                {
                    DisplayName = string.IsNullOrEmpty(RuleDisplayName) ? "Classic" : RuleDisplayName,
                    TargetScore = TargetScore,
                    SpadesMustBeBroken = SpadesMustBeBroken,
                    AllowSpadesAnytime = AllowSpadesAnytime,
                    FollowSuitRequired = FollowSuitRequired,
                    RenegePenaltyEnabled = RenegePenaltyEnabled
                }
            };

            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                state.SeatNames[seat] = SeatNames != null && i < SeatNames.Length && !string.IsNullOrEmpty(SeatNames[i])
                    ? SeatNames[i]
                    : seat.DisplayName();
            }

            if (Scores != null)
            {
                for (var i = 0; i < Scores.Length; i++)
                {
                    var score = Scores[i].ToSnapshot();
                    state.Scores[score.Team] = score;
                }
            }

            state.RoundState = new RoundState
            {
                RoundNumber = RoundNumber,
                Dealer = (SeatId)Dealer,
                LastStatusMessage = StatusMessage ?? string.Empty
            };
            state.RoundState.BidState.CurrentBidder = (SeatId)CurrentBidder;
            state.RoundState.TrickState.Leader = (SeatId)TrickLeader;
            state.RoundState.TrickState.CurrentTurn = (SeatId)CurrentTurn;
            state.RoundState.TrickState.LeadSuit = HasLeadSuit == 1 ? (Suit?)(Suit)LeadSuit : null;
            state.RoundState.TrickState.SpadesBroken = SpadesBroken == 1;

            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                state.RoundState.BidState.BidsBySeat[seat] = Bids != null && i < Bids.Length ? Bids[i] : null;
                state.RoundState.TricksWonBySeat[seat] = TricksWon != null && i < TricksWon.Length ? TricksWon[i] : 0;
                var count = HandCounts != null && i < HandCounts.Length ? HandCounts[i] : 0;
                if (seat == localSeat && localHand != null)
                {
                    state.RoundState.HandsBySeat[seat] = new List<Card>(localHand);
                }
                else
                {
                    var placeholder = new List<Card>(count);
                    for (var c = 0; c < count; c++)
                    {
                        placeholder.Add(new Card(Suit.Clubs, 2));
                    }

                    state.RoundState.HandsBySeat[seat] = placeholder;
                }
            }

            if (CurrentPlays != null)
            {
                for (var i = 0; i < CurrentPlays.Length; i++)
                {
                    state.RoundState.TrickState.Plays.Add(new TrickPlay
                    {
                        Seat = (SeatId)CurrentPlays[i].Seat,
                        Card = CurrentPlays[i].Card.ToCard()
                    });
                }
            }

            if (RenegeSeats != null)
            {
                for (var i = 0; i < RenegeSeats.Length; i++)
                {
                    state.RoundState.RenegeSeats.Add((SeatId)RenegeSeats[i]);
                }
            }

            return state;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref TargetScore);
            serializer.SerializeValue(ref HasWinningTeam);
            serializer.SerializeValue(ref WinningTeam);
            serializer.SerializeValue(ref StatusMessage);
            serializer.SerializeValue(ref RoundNumber);
            serializer.SerializeValue(ref Dealer);
            serializer.SerializeValue(ref CurrentBidder);
            serializer.SerializeValue(ref TrickLeader);
            serializer.SerializeValue(ref CurrentTurn);
            serializer.SerializeValue(ref HasLeadSuit);
            serializer.SerializeValue(ref LeadSuit);
            serializer.SerializeValue(ref SpadesBroken);
            serializer.SerializeValue(ref RuleDisplayName);
            serializer.SerializeValue(ref SpadesMustBeBroken);
            serializer.SerializeValue(ref AllowSpadesAnytime);
            serializer.SerializeValue(ref FollowSuitRequired);
            serializer.SerializeValue(ref RenegePenaltyEnabled);

            SerializeStringArray(serializer, ref SeatNames);
            SerializeNullableIntArray(serializer, ref Bids);
            SerializeIntArray(serializer, ref TricksWon);
            SerializeIntArray(serializer, ref HandCounts);
            SerializeScoreArray(serializer, ref Scores);
            SerializePlayArray(serializer, ref CurrentPlays);
            SerializeByteArray(serializer, ref RenegeSeats);
        }

        private static void SerializeStringArray<T>(BufferSerializer<T> serializer, ref string[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new string[length];
            }

            for (var i = 0; i < length; i++)
            {
                var value = values[i] ?? string.Empty;
                serializer.SerializeValue(ref value);
                values[i] = value;
            }
        }

        private static void SerializeNullableIntArray<T>(BufferSerializer<T> serializer, ref int?[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new int?[length];
            }

            for (var i = 0; i < length; i++)
            {
                var hasValue = values[i].HasValue;
                var raw = values[i] ?? 0;
                serializer.SerializeValue(ref hasValue);
                serializer.SerializeValue(ref raw);
                values[i] = hasValue ? raw : null;
            }
        }

        private static void SerializeIntArray<T>(BufferSerializer<T> serializer, ref int[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new int[length];
            }

            for (var i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref values[i]);
            }
        }

        private static void SerializeByteArray<T>(BufferSerializer<T> serializer, ref byte[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new byte[length];
            }

            for (var i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref values[i]);
            }
        }

        private static void SerializeScoreArray<T>(BufferSerializer<T> serializer, ref SpadesNetworkScore[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new SpadesNetworkScore[length];
            }

            for (var i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref values[i]);
            }
        }

        private static void SerializePlayArray<T>(BufferSerializer<T> serializer, ref SpadesNetworkTrickPlay[] values) where T : IReaderWriter
        {
            var length = values?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                values = new SpadesNetworkTrickPlay[length];
            }

            for (var i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref values[i]);
            }
        }
    }

    public struct SpadesNetworkEventPayload : INetworkSerializable
    {
        public byte Kind;
        public byte Seat;
        public byte Team;
        public byte WinningTeam;
        public int Bid;
        public int ClaimedBooks;
        public SpadesNetworkCard Card;
        public string Message;
        public SpadesNetworkPublicState PublicState;
        public SpadesNetworkTrickPlay[] CompletedTrick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref WinningTeam);
            serializer.SerializeValue(ref Bid);
            serializer.SerializeValue(ref ClaimedBooks);
            serializer.SerializeValue(ref Card);
            serializer.SerializeValue(ref Message);

            var hasState = PublicState != null;
            serializer.SerializeValue(ref hasState);
            if (hasState)
            {
                PublicState ??= new SpadesNetworkPublicState();
                PublicState.NetworkSerialize(serializer);
            }
            else if (serializer.IsReader)
            {
                PublicState = null;
            }

            var trickLength = CompletedTrick?.Length ?? 0;
            serializer.SerializeValue(ref trickLength);
            if (serializer.IsReader)
            {
                CompletedTrick = new SpadesNetworkTrickPlay[trickLength];
            }

            for (var i = 0; i < trickLength; i++)
            {
                serializer.SerializeValue(ref CompletedTrick[i]);
            }
        }
    }
}
