using System;
using System.Collections.Generic;
using System.Linq;

namespace BackyardLegends.Core
{
    public interface IRuleEngine
    {
        IReadOnlyList<int> GetLegalBids(MatchState matchState, SeatId seat);
        IReadOnlyList<Card> GetLegalCards(MatchState matchState, SeatId seat);
        SeatId ResolveTrickWinner(TrickState trickState);
        RoundScoreResult ScoreRound(MatchState matchState);
    }

    public sealed class TeamRoundScore
    {
        public TeamId Team;
        public int ContractBid;
        public int TricksWon;
        public int RoundDelta;
        public int NilDelta;
        public int RenegeDelta;
        public int BagsEarned;
        public int BagPenaltyDelta;
        public int BagsAfterRound;
    }

    public sealed class RoundScoreResult
    {
        public RoundScoreResult()
        {
            TeamScores = new Dictionary<TeamId, TeamRoundScore>();
        }

        public Dictionary<TeamId, TeamRoundScore> TeamScores { get; }
        public TeamId? WinningTeam;
        public string Summary = string.Empty;
    }

    public sealed class SpadesRuleEngine : IRuleEngine
    {
        public IReadOnlyList<int> GetLegalBids(MatchState matchState, SeatId seat)
        {
            var rules = matchState.RuleSet;
            var round = matchState.RoundState;
            var legal = new List<int>();
            var partner = seat.Partner();
            var partnerBid = round.BidState.BidsBySeat.TryGetValue(partner, out var value) ? value : null;
            var team = seat.ToTeam();

            var teamScore = matchState.Scores[team].Score;
            var opponentScore = matchState.Scores[team == TeamId.Home ? TeamId.Away : TeamId.Home].Score;
            var canBidNil = opponentScore - teamScore >= rules.NilUnlockScoreGap;

            for (var bid = 1; bid <= rules.MaxBid; bid++)
            {
                if (!ViolatesMinimumTeamBid(rules, bid, partnerBid))
                {
                    legal.Add(bid);
                }
            }

            if (canBidNil && !ViolatesMinimumTeamBid(rules, 0, partnerBid))
            {
                legal.Insert(0, 0);
            }

            if (legal.Count == 0)
            {
                var forcedBid = partnerBid.HasValue
                    ? Math.Max(1, rules.MinimumTeamBid - partnerBid.Value)
                    : rules.MinimumTeamBid;
                legal.Add(Math.Clamp(forcedBid, 1, rules.MaxBid));
            }

            return legal;
        }

        public IReadOnlyList<Card> GetLegalCards(MatchState matchState, SeatId seat)
        {
            var rules = matchState.RuleSet;
            var round = matchState.RoundState;
            var hand = round.HandsBySeat[seat];
            if (round.TrickState.Plays.Count == 0)
            {
                if (!rules.SpadesMustBeBroken || rules.AllowSpadesAnytime || round.TrickState.SpadesBroken)
                {
                    return hand.ToList();
                }

                var nonSpades = hand.Where(card => card.Suit != Suit.Spades).ToList();
                return nonSpades.Count > 0 ? nonSpades : hand.ToList();
            }

            if (!rules.FollowSuitRequired || !round.TrickState.LeadSuit.HasValue)
            {
                return hand.ToList();
            }

            var leadSuitCards = hand.Where(card => card.Suit == round.TrickState.LeadSuit.Value).ToList();
            return leadSuitCards.Count > 0 ? leadSuitCards : hand.ToList();
        }

        public SeatId ResolveTrickWinner(TrickState trickState)
        {
            if (trickState.Plays.Count == 0)
            {
                throw new InvalidOperationException("Cannot resolve an empty trick.");
            }

            var leadSuit = trickState.LeadSuit ?? trickState.Plays[0].Card.Suit;
            var winner = trickState.Plays[0];

            foreach (var play in trickState.Plays.Skip(1))
            {
                if (Beats(play.Card, winner.Card, leadSuit))
                {
                    winner = play;
                }
            }

            return winner.Seat;
        }

        public RoundScoreResult ScoreRound(MatchState matchState)
        {
            var result = new RoundScoreResult();
            var rules = matchState.RuleSet;
            var summaryLines = new List<string>();

            foreach (var team in new[] { TeamId.Home, TeamId.Away })
            {
                var tricksWon = matchState.RoundState.TricksWonBySeat
                    .Where(entry => entry.Key.ToTeam() == team)
                    .Sum(entry => entry.Value);
                var teamBid = matchState.RoundState.BidState.BidsBySeat
                    .Where(entry => entry.Key.ToTeam() == team && entry.Value.HasValue)
                    .Sum(entry => entry.Value ?? 0);

                var nilDelta = 0;
                foreach (var nilSeat in matchState.RoundState.BidState.BidsBySeat
                             .Where(entry => entry.Key.ToTeam() == team && entry.Value == 0)
                             .Select(entry => entry.Key))
                {
                    var nilSucceeded = matchState.RoundState.TricksWonBySeat[nilSeat] == 0;
                    nilDelta += nilSucceeded ? rules.NilScore : -rules.NilScore;
                }

                var score = matchState.Scores[team];
                var bagsEarned = 0;
                var bagPenaltyDelta = 0;
                var roundDelta = 0;
                var renegeDelta = 0;
                if (tricksWon >= teamBid)
                {
                    roundDelta += teamBid * 10;
                    bagsEarned = tricksWon - teamBid;
                    roundDelta += bagsEarned;
                }
                else
                {
                    roundDelta -= teamBid * 10;
                }

                score.Bags += bagsEarned;
                if (score.Bags >= rules.BagPenaltyThreshold)
                {
                    var penalties = score.Bags / rules.BagPenaltyThreshold;
                    bagPenaltyDelta = penalties * rules.BagPenaltyPoints;
                    roundDelta += bagPenaltyDelta;
                    score.Bags %= rules.BagPenaltyThreshold;
                }

                if (rules.RenegePenaltyEnabled)
                {
                    var reneges = matchState.RoundState.RenegeSeats.Count(seat => seat.ToTeam() == team);
                    if (reneges > 0)
                    {
                        renegeDelta = reneges * rules.RenegePenaltyPoints;
                        roundDelta += renegeDelta;
                    }
                }

                score.ContractBid = teamBid;
                score.TricksWon = tricksWon;
                score.RoundDelta = roundDelta;
                score.NilDelta = nilDelta;
                score.BagsEarned = bagsEarned;
                score.BagPenaltyDelta = bagPenaltyDelta;
                score.Score += roundDelta + nilDelta;

                result.TeamScores[team] = new TeamRoundScore
                {
                    Team = team,
                    ContractBid = teamBid,
                    TricksWon = tricksWon,
                    RoundDelta = roundDelta,
                    NilDelta = nilDelta,
                    RenegeDelta = renegeDelta,
                    BagsEarned = bagsEarned,
                    BagPenaltyDelta = bagPenaltyDelta,
                    BagsAfterRound = score.Bags
                };

                summaryLines.Add($"{team}: bid {teamBid}, took {tricksWon}, bags +{bagsEarned}, bag penalty {bagPenaltyDelta:+#;-#;0}, round {roundDelta:+#;-#;0}, nil {nilDelta:+#;-#;0}, renege {renegeDelta:+#;-#;0}");
            }

            var homeScore = matchState.Scores[TeamId.Home].Score;
            var awayScore = matchState.Scores[TeamId.Away].Score;
            if (homeScore >= rules.TargetScore || awayScore >= rules.TargetScore)
            {
                if (homeScore != awayScore)
                {
                    result.WinningTeam = homeScore > awayScore ? TeamId.Home : TeamId.Away;
                }
            }

            result.Summary = string.Join("\n", summaryLines);
            return result;
        }

        private static bool ViolatesMinimumTeamBid(RuleSetDefinition rules, int bid, int? partnerBid)
        {
            if (!partnerBid.HasValue)
            {
                return false;
            }

            return partnerBid.Value + bid < rules.MinimumTeamBid;
        }

        private static bool Beats(Card challenger, Card currentWinner, Suit leadSuit)
        {
            if (challenger.Suit == currentWinner.Suit)
            {
                return challenger.Rank > currentWinner.Rank;
            }

            if (challenger.Suit == Suit.Spades && currentWinner.Suit != Suit.Spades)
            {
                return true;
            }

            if (challenger.Suit != Suit.Spades && currentWinner.Suit == Suit.Spades)
            {
                return false;
            }

            return challenger.Suit == leadSuit && currentWinner.Suit != leadSuit;
        }
    }
}
