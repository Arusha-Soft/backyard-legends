using System.Collections.Generic;
using System.Linq;

namespace BackyardLegends.Core
{
    public abstract class SpadesMatchEvent
    {
        protected SpadesMatchEvent(MatchState snapshot)
        {
            Snapshot = snapshot;
        }

        public MatchState Snapshot { get; }
    }

    public sealed class MatchStartedEvent : SpadesMatchEvent
    {
        public MatchStartedEvent(MatchState snapshot) : base(snapshot) { }
    }

    public sealed class RoundStartedEvent : SpadesMatchEvent
    {
        public RoundStartedEvent(MatchState snapshot) : base(snapshot) { }
    }

    public sealed class BidSubmittedEvent : SpadesMatchEvent
    {
        public BidSubmittedEvent(MatchState snapshot, SeatId seat, int bid) : base(snapshot)
        {
            Seat = seat;
            Bid = bid;
        }

        public SeatId Seat { get; }
        public int Bid { get; }
    }

    public sealed class CardPlayedEvent : SpadesMatchEvent
    {
        public CardPlayedEvent(MatchState snapshot, SeatId seat, Card card) : base(snapshot)
        {
            Seat = seat;
            Card = card;
        }

        public SeatId Seat { get; }
        public Card Card { get; }
    }

    public sealed class TrickResolvedEvent : SpadesMatchEvent
    {
        public TrickResolvedEvent(MatchState snapshot, SeatId winner, IReadOnlyList<TrickPlay> completedTrick) : base(snapshot)
        {
            Winner = winner;
            CompletedTrick = completedTrick
                .Select(play => new TrickPlay
                {
                    Seat = play.Seat,
                    Card = play.Card
                })
                .ToList();
        }

        public SeatId Winner { get; }
        public IReadOnlyList<TrickPlay> CompletedTrick { get; }
    }

    public sealed class RoundScoredEvent : SpadesMatchEvent
    {
        public RoundScoredEvent(MatchState snapshot, string roundSummary) : base(snapshot)
        {
            RoundSummary = roundSummary;
        }

        public string RoundSummary { get; }
    }

    public sealed class MatchEndedEvent : SpadesMatchEvent
    {
        public MatchEndedEvent(MatchState snapshot, TeamId winningTeam) : base(snapshot)
        {
            WinningTeam = winningTeam;
        }

        public TeamId WinningTeam { get; }
    }

    public sealed class SetBookReachedEvent : SpadesMatchEvent
    {
        public SetBookReachedEvent(MatchState snapshot, TeamId team) : base(snapshot)
        {
            Team = team;
        }

        public TeamId Team { get; }
    }
}
