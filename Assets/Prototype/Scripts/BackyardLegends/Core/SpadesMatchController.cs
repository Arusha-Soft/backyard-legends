using System;
using System.Collections.Generic;
using System.Linq;

namespace BackyardLegends.Core
{
    public sealed class SpadesMatchController
    {
        private readonly IRuleEngine ruleEngine;
        private readonly Dictionary<SeatId, IAiAgent> aiAgents;
        private readonly Random random;

        public SpadesMatchController(
            RuleSetDefinition rules,
            IRuleEngine ruleEngine,
            Dictionary<SeatId, IAiAgent> aiAgents,
            int? seed = null)
        {
            this.ruleEngine = ruleEngine;
            this.aiAgents = aiAgents;
            random = seed.HasValue ? new Random(seed.Value) : new Random();

            State = new MatchState
            {
                RuleSet = rules,
                TargetScore = rules.TargetScore,
                Phase = MatchPhase.Lobby
            };

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                State.SeatNames[seat] = seat.DisplayName();
            }
        }

        public event Action<SpadesMatchEvent> EventRaised;

        public MatchState State { get; }
        public SeatId HumanSeat => SeatId.Bottom;
        public bool NeedsAiTurn =>
            (State.Phase == MatchPhase.Bidding && State.RoundState.BidState.CurrentBidder != HumanSeat) ||
            (State.Phase == MatchPhase.TrickPlay && State.RoundState.TrickState.CurrentTurn != HumanSeat);

        public void StartMatch()
        {
            ResetScoreState();
            Raise(new MatchStartedEvent(CreateSnapshot()));
            StartRoundInternal();
        }

        public void StartNextRound()
        {
            if (State.Phase != MatchPhase.RoundSummary)
            {
                return;
            }

            StartRoundInternal();
        }

        public IReadOnlyList<int> GetLegalBidsForSeat(SeatId seat)
        {
            return ruleEngine.GetLegalBids(State, seat);
        }

        public IReadOnlyList<Card> GetLegalCardsForSeat(SeatId seat)
        {
            return ruleEngine.GetLegalCards(State, seat);
        }

        public IReadOnlyList<Card> GetHand(SeatId seat)
        {
            return State.RoundState.HandsBySeat[seat].ToList();
        }

        public bool TrySubmitBid(SeatId seat, int bid, out string error)
        {
            error = string.Empty;
            if (State.Phase != MatchPhase.Bidding)
            {
                error = "Bidding is closed.";
                return false;
            }

            if (State.RoundState.BidState.CurrentBidder != seat)
            {
                error = "It is not that seat's bid turn.";
                return false;
            }

            var legalBids = GetLegalBidsForSeat(seat);
            if (!legalBids.Contains(bid))
            {
                error = "That bid is not legal in the current team state.";
                return false;
            }

            State.RoundState.BidState.BidsBySeat[seat] = bid;
            State.RoundState.LastStatusMessage = $"{State.SeatNames[seat]} bid {BidLabel(bid)}.";
            Raise(new BidSubmittedEvent(CreateSnapshot(), seat, bid));

            if (AllBidsSubmitted())
            {
                State.Phase = MatchPhase.TrickPlay;
                var openingLeader = State.RoundState.Dealer.NextClockwise();
                State.RoundState.TrickState.Leader = openingLeader;
                State.RoundState.TrickState.CurrentTurn = openingLeader;
                State.RoundState.LastStatusMessage = $"{State.SeatNames[openingLeader]} leads the first trick.";
                return true;
            }

            State.RoundState.BidState.CurrentBidder = seat.NextClockwise();
            return true;
        }

        public bool TryPlayCard(SeatId seat, Card card, out string error)
        {
            error = string.Empty;
            if (State.Phase != MatchPhase.TrickPlay)
            {
                error = "Cards are not live right now.";
                return false;
            }

            if (State.RoundState.TrickState.CurrentTurn != seat)
            {
                error = "It is not that seat's turn.";
                return false;
            }

            var legalCards = GetLegalCardsForSeat(seat);
            if (!legalCards.Contains(card))
            {
                error = "That card breaks the active trick rules.";
                return false;
            }

            MaybeRecordRenege(seat, card);
            State.RoundState.HandsBySeat[seat].Remove(card);
            var trickState = State.RoundState.TrickState;
            if (trickState.Plays.Count == 0)
            {
                trickState.Leader = seat;
                trickState.LeadSuit = card.Suit;
            }

            if (card.Suit == Suit.Spades)
            {
                trickState.SpadesBroken = true;
            }

            trickState.Plays.Add(new TrickPlay { Seat = seat, Card = card });
            State.RoundState.LastStatusMessage = $"{State.SeatNames[seat]} played {card.ShortLabel}.";
            Raise(new CardPlayedEvent(CreateSnapshot(), seat, card));

            if (trickState.Plays.Count < 4)
            {
                trickState.CurrentTurn = seat.NextClockwise();
                return true;
            }

            ResolveCurrentTrick();
            return true;
        }

        public bool TryClaimRemainingBooks(TeamId claimingTeam, out string error)
        {
            error = string.Empty;
            if (State.Phase != MatchPhase.TrickPlay)
            {
                error = "You can only claim the rest while cards are live.";
                return false;
            }

            var remainingBooks = GetRemainingBookCount();
            if (remainingBooks <= 0)
            {
                error = "There are no books left to claim.";
                return false;
            }

            var awardSeat = ChooseClaimAwardSeat(claimingTeam);
            State.RoundState.TricksWonBySeat[awardSeat] += remainingBooks;
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                State.RoundState.HandsBySeat[seat].Clear();
            }

            State.RoundState.TrickState.Plays.Clear();
            State.RoundState.TrickState.LeadSuit = null;
            State.RoundState.TrickState.Leader = awardSeat;
            State.RoundState.TrickState.CurrentTurn = awardSeat;
            State.RoundState.LastStatusMessage = $"{TeamLabel(claimingTeam)} claimed the remaining {remainingBooks} {BookLabel(remainingBooks)}.";
            State.Phase = MatchPhase.RoundSummary;
            Raise(new RemainingBooksClaimedEvent(CreateSnapshot(), claimingTeam, remainingBooks));
            ScoreCurrentRound();
            return true;
        }

        public bool TryForfeitMatch(TeamId forfeitingTeam, out string error)
        {
            error = string.Empty;
            if (State.Phase != MatchPhase.Bidding && State.Phase != MatchPhase.TrickPlay)
            {
                error = "You can only forfeit an active match.";
                return false;
            }

            var winningTeam = forfeitingTeam == TeamId.Home ? TeamId.Away : TeamId.Home;
            State.Phase = MatchPhase.MatchEnded;
            State.WinningTeam = winningTeam;
            if (State.RoundState != null)
            {
                State.RoundState.TrickState.Plays.Clear();
                State.RoundState.TrickState.LeadSuit = null;
                State.RoundState.RenegeSeats.Clear();
                State.RoundState.LastStatusMessage = $"{TeamLabel(forfeitingTeam)} forfeited the match.";
            }

            Raise(new MatchForfeitedEvent(CreateSnapshot(), forfeitingTeam, winningTeam));
            Raise(new MatchEndedEvent(CreateSnapshot(), winningTeam));
            return true;
        }

        private void MaybeRecordRenege(SeatId seat, Card card)
        {
            if (!State.RuleSet.RenegePenaltyEnabled)
            {
                return;
            }

            var trickState = State.RoundState.TrickState;
            if (trickState.Plays.Count == 0 || !trickState.LeadSuit.HasValue || card.Suit == trickState.LeadSuit.Value)
            {
                return;
            }

            var hasLeadSuit = State.RoundState.HandsBySeat[seat].Any(heldCard => heldCard.Suit == trickState.LeadSuit.Value);
            if (!hasLeadSuit || State.RoundState.RenegeSeats.Contains(seat))
            {
                return;
            }

            State.RoundState.RenegeSeats.Add(seat);
            State.RoundState.LastStatusMessage = $"{State.SeatNames[seat]} reneged. A -200 penalty is locked for the round.";
        }

        public void AdvanceAiTurn()
        {
            if (!NeedsAiTurn)
            {
                return;
            }

            if (State.Phase == MatchPhase.Bidding)
            {
                var seat = State.RoundState.BidState.CurrentBidder;
                var context = new AiBidContext
                {
                    Seat = seat,
                    HumanSeat = HumanSeat,
                    MatchState = CreateSnapshot(),
                    Hand = GetHand(seat),
                    LegalBids = GetLegalBidsForSeat(seat)
                };
                var bid = aiAgents[seat].ChooseBid(context);
                TrySubmitBid(seat, bid, out _);
                return;
            }

            if (State.Phase == MatchPhase.TrickPlay)
            {
                var seat = State.RoundState.TrickState.CurrentTurn;
                var context = new AiPlayContext
                {
                    Seat = seat,
                    MatchState = CreateSnapshot(),
                    Hand = GetHand(seat),
                    LegalCards = GetLegalCardsForSeat(seat)
                };
                var card = aiAgents[seat].ChooseCard(context);
                TryPlayCard(seat, card, out _);
            }
        }

        public int GetTeamBid(TeamId team)
        {
            return State.RoundState.BidState.BidsBySeat
                .Where(entry => entry.Key.ToTeam() == team && entry.Value.HasValue)
                .Sum(entry => entry.Value ?? 0);
        }

        public int GetTeamTricks(TeamId team)
        {
            return State.RoundState.TricksWonBySeat
                .Where(entry => entry.Key.ToTeam() == team)
                .Sum(entry => entry.Value);
        }

        public int GetRemainingBookCount()
        {
            if (State.RoundState == null || State.RoundState.HandsBySeat.Count == 0)
            {
                return 0;
            }

            return State.RoundState.HandsBySeat.Values.Max(hand => hand.Count);
        }

        public string DescribeLastTrick()
        {
            var lastTrick = State.RoundState.CompletedTricks.LastOrDefault();
            if (lastTrick == null || lastTrick.Count == 0)
            {
                return "No tricks resolved yet.";
            }

            return string.Join(" | ", lastTrick.Select(play => $"{State.SeatNames[play.Seat]} {play.Card.ShortLabel}"));
        }

        private void ResetScoreState()
        {
            State.Scores[TeamId.Home] = new ScoreSnapshot { Team = TeamId.Home };
            State.Scores[TeamId.Away] = new ScoreSnapshot { Team = TeamId.Away };
            State.WinningTeam = null;
        }

        private void StartRoundInternal()
        {
            State.RoundState = new RoundState
            {
                RoundNumber = State.RoundState?.RoundNumber + 1 ?? 1
            };

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                State.RoundState.HandsBySeat[seat] = new List<Card>();
                State.RoundState.TricksWonBySeat[seat] = 0;
                State.RoundState.BidState.BidsBySeat[seat] = null;
            }

            State.Phase = MatchPhase.Bidding;
            State.RoundState.Dealer = SpadesSeatUtility.TurnOrder[(State.RoundState.RoundNumber - 1) % 4];
            var openingBidder = HumanSeat.NextClockwise();
            var openingLeader = State.RoundState.Dealer.NextClockwise();
            State.RoundState.BidState.CurrentBidder = openingBidder;
            State.RoundState.TrickState.Leader = openingLeader;
            State.RoundState.TrickState.CurrentTurn = openingBidder;
            State.RoundState.TrickState.SpadesBroken = false;
            State.RoundState.LastStatusMessage = $"{State.SeatNames[State.RoundState.BidState.CurrentBidder]} starts the bidding.";

            var deck = SpadesDeckUtility.CreateDeck();
            SpadesDeckUtility.Shuffle(deck, random);
            DealDeck(deck);

            Raise(new RoundStartedEvent(CreateSnapshot()));
        }

        private void DealDeck(List<Card> deck)
        {
            for (var i = 0; i < deck.Count; i++)
            {
                var seat = SpadesSeatUtility.TurnOrder[i % 4];
                State.RoundState.HandsBySeat[seat].Add(deck[i]);
            }

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                State.RoundState.HandsBySeat[seat] = SpadesDeckUtility.SortHand(State.RoundState.HandsBySeat[seat]);
            }
        }

        private bool AllBidsSubmitted()
        {
            return State.RoundState.BidState.BidsBySeat.All(entry => entry.Value.HasValue);
        }

        private SeatId ChooseClaimAwardSeat(TeamId claimingTeam)
        {
            return SpadesSeatUtility.TurnOrder
                .Where(seat => seat.ToTeam() == claimingTeam)
                .OrderBy(seat => State.RoundState.BidState.BidsBySeat.TryGetValue(seat, out var bid) && bid == 0 ? 1 : 0)
                .ThenBy(seat => seat)
                .First();
        }

        private static string TeamLabel(TeamId team)
        {
            return team == TeamId.Home ? "Home team" : "Away team";
        }

        private static string BookLabel(int count)
        {
            return count == 1 ? "book" : "books";
        }

        private void ResolveCurrentTrick()
        {
            var completedTrick = State.RoundState.TrickState.Plays
                .Select(play => new TrickPlay { Seat = play.Seat, Card = play.Card })
                .ToList();
            var winner = ruleEngine.ResolveTrickWinner(State.RoundState.TrickState);
            State.RoundState.TricksWonBySeat[winner] += 1;
            State.RoundState.CompletedTricks.Add(completedTrick);
            State.RoundState.LastStatusMessage = $"{State.SeatNames[winner]} took the trick.";

            State.RoundState.TrickState.Plays.Clear();
            State.RoundState.TrickState.LeadSuit = null;
            State.RoundState.TrickState.Leader = winner;
            State.RoundState.TrickState.CurrentTurn = winner;
            Raise(new TrickResolvedEvent(CreateSnapshot(), winner, completedTrick));

            var roundComplete = SpadesSeatUtility.TurnOrder.All(seat => State.RoundState.HandsBySeat[seat].Count == 0);
            if (!roundComplete)
            {
                return;
            }

            ScoreCurrentRound();
        }

        private void ScoreCurrentRound()
        {
            var result = ruleEngine.ScoreRound(State);
            State.Phase = result.WinningTeam.HasValue ? MatchPhase.MatchEnded : MatchPhase.RoundSummary;
            State.WinningTeam = result.WinningTeam;
            State.RoundState.LastStatusMessage = result.Summary;
            Raise(new RoundScoredEvent(CreateSnapshot(), result.Summary));
            foreach (var teamScore in result.TeamScores.Values.Where(teamScore => teamScore.ContractBid > 0 && teamScore.TricksWon < teamScore.ContractBid))
            {
                Raise(new SetBookReachedEvent(CreateSnapshot(), teamScore.Team));
            }

            if (result.WinningTeam.HasValue)
            {
                Raise(new MatchEndedEvent(CreateSnapshot(), result.WinningTeam.Value));
            }
        }

        private void Raise(SpadesMatchEvent matchEvent)
        {
            EventRaised?.Invoke(matchEvent);
        }

        private MatchState CreateSnapshot()
        {
            var snapshot = new MatchState
            {
                Phase = State.Phase,
                TargetScore = State.TargetScore,
                RuleSet = State.RuleSet.CloneForTarget(State.RuleSet.TargetScore),
                WinningTeam = State.WinningTeam
            };

            foreach (var name in State.SeatNames)
            {
                snapshot.SeatNames[name.Key] = name.Value;
            }

            foreach (var score in State.Scores)
            {
                snapshot.Scores[score.Key] = new ScoreSnapshot
                {
                    Team = score.Value.Team,
                    Score = score.Value.Score,
                    Bags = score.Value.Bags,
                    ContractBid = score.Value.ContractBid,
                    TricksWon = score.Value.TricksWon,
                    RoundDelta = score.Value.RoundDelta,
                    NilDelta = score.Value.NilDelta,
                    BagsEarned = score.Value.BagsEarned,
                    BagPenaltyDelta = score.Value.BagPenaltyDelta
                };
            }

            if (State.RoundState == null)
            {
                return snapshot;
            }

            snapshot.RoundState = new RoundState
            {
                RoundNumber = State.RoundState.RoundNumber,
                Dealer = State.RoundState.Dealer,
                LastStatusMessage = State.RoundState.LastStatusMessage
            };

            snapshot.RoundState.BidState.CurrentBidder = State.RoundState.BidState.CurrentBidder;
            foreach (var bid in State.RoundState.BidState.BidsBySeat)
            {
                snapshot.RoundState.BidState.BidsBySeat[bid.Key] = bid.Value;
            }

            snapshot.RoundState.TrickState.Leader = State.RoundState.TrickState.Leader;
            snapshot.RoundState.TrickState.CurrentTurn = State.RoundState.TrickState.CurrentTurn;
            snapshot.RoundState.TrickState.LeadSuit = State.RoundState.TrickState.LeadSuit;
            snapshot.RoundState.TrickState.SpadesBroken = State.RoundState.TrickState.SpadesBroken;
            foreach (var play in State.RoundState.TrickState.Plays)
            {
                snapshot.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = play.Seat, Card = play.Card });
            }

            foreach (var hand in State.RoundState.HandsBySeat)
            {
                snapshot.RoundState.HandsBySeat[hand.Key] = hand.Value.ToList();
            }

            foreach (var trick in State.RoundState.TricksWonBySeat)
            {
                snapshot.RoundState.TricksWonBySeat[trick.Key] = trick.Value;
            }

            foreach (var completed in State.RoundState.CompletedTricks)
            {
                snapshot.RoundState.CompletedTricks.Add(completed.Select(play => new TrickPlay
                {
                    Seat = play.Seat,
                    Card = play.Card
                }).ToList());
            }

            foreach (var seat in State.RoundState.RenegeSeats)
            {
                snapshot.RoundState.RenegeSeats.Add(seat);
            }

            return snapshot;
        }

        private static string BidLabel(int bid)
        {
            return bid == 0 ? "Nil" : bid.ToString();
        }
    }
}
