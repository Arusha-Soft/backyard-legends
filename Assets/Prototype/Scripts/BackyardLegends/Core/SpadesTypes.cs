using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BackyardLegends.Core
{
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    public enum SeatId
    {
        Bottom = 0,
        Left = 1,
        Top = 2,
        Right = 3
    }

    public enum TeamId
    {
        Home,
        Away
    }

    public enum MatchPhase
    {
        Lobby,
        Bidding,
        TrickPlay,
        RoundSummary,
        MatchEnded
    }

    public readonly struct Card : IEquatable<Card>, IComparable<Card>
    {
        public Card(Suit suit, int rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public Suit Suit { get; }
        public int Rank { get; }

        public string ShortLabel => $"{RankLabel}{SuitIcon}";
        public string RankLabel => Rank switch
        {
            14 => "A",
            13 => "K",
            12 => "Q",
            11 => "J",
            _ => Rank.ToString()
        };

        public string SuitIcon => Suit switch
        {
            Suit.Clubs => "\u2663",
            Suit.Diamonds => "\u2666",
            Suit.Hearts => "\u2665",
            Suit.Spades => "\u2660",
            _ => "?"
        };

        public bool IsRed => Suit == Suit.Diamonds || Suit == Suit.Hearts;

        public int CompareTo(Card other)
        {
            var suitOrder = Suit.CompareTo(other.Suit);
            return suitOrder != 0 ? suitOrder : Rank.CompareTo(other.Rank);
        }

        public bool Equals(Card other)
        {
            return Suit == other.Suit && Rank == other.Rank;
        }

        public override bool Equals(object obj)
        {
            return obj is Card other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Suit, Rank);
        }

        public override string ToString()
        {
            return ShortLabel;
        }
    }

    [Serializable]
    public sealed class RuleSetDefinition
    {
        public string DisplayName = "Classic";
        public bool SpadesMustBeBroken = true;
        public bool AllowSpadesAnytime;
        public bool FollowSuitRequired = true;
        public bool RenegePenaltyEnabled;
        public int RenegePenaltyPoints = -200;
        public int NilScore = 100;
        public int NilUnlockScoreGap = 150;
        public int MinimumTeamBid = 4;
        public int MaxBid = 13;
        public int BagPenaltyThreshold = 10;
        public int BagPenaltyPoints = -100;
        public int TargetScore = 100;
        public bool EnableFutureTurnTimer;
        public int ReservedTurnTimerSeconds = 30;

        public RuleSetDefinition CloneForTarget(int targetScore)
        {
            return new RuleSetDefinition
            {
                DisplayName = DisplayName,
                SpadesMustBeBroken = SpadesMustBeBroken,
                AllowSpadesAnytime = AllowSpadesAnytime,
                FollowSuitRequired = FollowSuitRequired,
                RenegePenaltyEnabled = RenegePenaltyEnabled,
                RenegePenaltyPoints = RenegePenaltyPoints,
                NilScore = NilScore,
                NilUnlockScoreGap = NilUnlockScoreGap,
                MinimumTeamBid = MinimumTeamBid,
                MaxBid = MaxBid,
                BagPenaltyThreshold = BagPenaltyThreshold,
                BagPenaltyPoints = BagPenaltyPoints,
                TargetScore = targetScore,
                EnableFutureTurnTimer = EnableFutureTurnTimer,
                ReservedTurnTimerSeconds = ReservedTurnTimerSeconds
            };
        }
    }

    [Serializable]
    public sealed class ScoreSnapshot
    {
        public TeamId Team;
        public int Score;
        public int Bags;
        public int ContractBid;
        public int TricksWon;
        public int RoundDelta;
        public int NilDelta;
        public int RenegeDelta;
        public int BagsEarned;
        public int BagPenaltyDelta;
    }

    [Serializable]
    public sealed class BidState
    {
        public BidState()
        {
            BidsBySeat = new Dictionary<SeatId, int?>();
        }

        public SeatId CurrentBidder;
        public Dictionary<SeatId, int?> BidsBySeat;
    }

    [Serializable]
    public sealed class TrickPlay
    {
        public SeatId Seat;
        public Card Card;

        public override string ToString()
        {
            return $"{Seat}: {Card.ShortLabel}";
        }
    }

    [Serializable]
    public sealed class TrickState
    {
        public TrickState()
        {
            Plays = new List<TrickPlay>();
        }

        public SeatId Leader;
        public SeatId CurrentTurn;
        public Suit? LeadSuit;
        public bool SpadesBroken;
        public List<TrickPlay> Plays;
    }

    [Serializable]
    public sealed class RoundState
    {
        public RoundState()
        {
            HandsBySeat = new Dictionary<SeatId, List<Card>>();
            TricksWonBySeat = new Dictionary<SeatId, int>();
            CompletedTricks = new List<List<TrickPlay>>();
            RenegeSeats = new List<SeatId>();
            BidState = new BidState();
            TrickState = new TrickState();
        }

        public int RoundNumber;
        public SeatId Dealer;
        public BidState BidState;
        public TrickState TrickState;
        public Dictionary<SeatId, List<Card>> HandsBySeat;
        public Dictionary<SeatId, int> TricksWonBySeat;
        public List<List<TrickPlay>> CompletedTricks;
        public List<SeatId> RenegeSeats;
        public string LastStatusMessage = string.Empty;
    }

    [Serializable]
    public sealed class MatchState
    {
        public MatchState()
        {
            Scores = new Dictionary<TeamId, ScoreSnapshot>();
            SeatNames = new Dictionary<SeatId, string>();
        }

        public MatchPhase Phase;
        public RuleSetDefinition RuleSet;
        public int TargetScore;
        public TeamId? WinningTeam;
        public Dictionary<TeamId, ScoreSnapshot> Scores;
        public RoundState RoundState;
        public Dictionary<SeatId, string> SeatNames;
    }

    public static class SpadesSeatUtility
    {
        public static readonly SeatId[] TurnOrder = { SeatId.Bottom, SeatId.Left, SeatId.Top, SeatId.Right };

        public static TeamId ToTeam(this SeatId seat)
        {
            return seat == SeatId.Bottom || seat == SeatId.Top ? TeamId.Home : TeamId.Away;
        }

        public static SeatId NextClockwise(this SeatId seat)
        {
            return (SeatId)(((int)seat + 1) % 4);
        }

        public static SeatId Partner(this SeatId seat)
        {
            return seat switch
            {
                SeatId.Bottom => SeatId.Top,
                SeatId.Top => SeatId.Bottom,
                SeatId.Left => SeatId.Right,
                SeatId.Right => SeatId.Left,
                _ => SeatId.Top
            };
        }

        public static string DisplayName(this SeatId seat)
        {
            return seat switch
            {
                SeatId.Bottom => "You",
                SeatId.Top => "Partner",
                SeatId.Left => "Opponent L",
                SeatId.Right => "Opponent R",
                _ => seat.ToString()
            };
        }

        public static Color Accent(this SeatId seat)
        {
            return seat switch
            {
                SeatId.Bottom => new Color(0.92f, 0.78f, 0.28f, 1f),
                SeatId.Top => new Color(0.22f, 0.73f, 0.48f, 1f),
                _ => new Color(0.85f, 0.3f, 0.28f, 1f)
            };
        }
    }

    public static class SpadesDeckUtility
    {
        public static List<Card> CreateDeck()
        {
            var deck = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (var rank = 2; rank <= 14; rank++)
                {
                    deck.Add(new Card(suit, rank));
                }
            }

            return deck;
        }

        public static void Shuffle(List<Card> deck, System.Random random)
        {
            for (var i = deck.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(0, i + 1);
                (deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
            }
        }

        public static List<Card> SortHand(IEnumerable<Card> cards)
        {
            return cards.OrderBy(card => card.Suit == Suit.Spades ? 4 : (int)card.Suit)
                .ThenBy(card => card.Rank)
                .ToList();
        }
    }
}
