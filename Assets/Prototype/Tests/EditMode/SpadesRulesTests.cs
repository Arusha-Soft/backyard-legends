using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using NUnit.Framework;

namespace BackyardLegends.Tests
{
    public sealed class SpadesRulesTests
    {
        [Test]
        public void DeckContains52UniqueCards()
        {
            var deck = SpadesDeckUtility.CreateDeck();

            Assert.That(deck, Has.Count.EqualTo(52));
            Assert.That(deck.Distinct().Count(), Is.EqualTo(52));
        }

        [Test]
        public void LegalCardsFollowSuitWhenPossible()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 4, 4, 4, 4);

            var round = controller.State.RoundState;
            round.HandsBySeat[SeatId.Bottom] = new List<Card>
            {
                new(Suit.Hearts, 5),
                new(Suit.Clubs, 4),
                new(Suit.Spades, 10)
            };
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 2) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 3) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 4) };
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Left;

            Assert.That(controller.TryPlayCard(SeatId.Left, round.HandsBySeat[SeatId.Left][0], out _), Is.True);
            var legal = controller.GetLegalCardsForSeat(SeatId.Top);
            Assert.That(legal.All(card => card.Suit == Suit.Hearts), Is.True);
        }

        [Test]
        public void ClassicModePreventsLeadingSpadesBeforeBroken()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 4, 4, 4, 4);

            var round = controller.State.RoundState;
            round.TrickState.Plays.Clear();
            round.TrickState.SpadesBroken = false;
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card>
            {
                new(Suit.Spades, 14),
                new(Suit.Hearts, 2)
            };

            var legal = controller.GetLegalCardsForSeat(SeatId.Bottom);
            Assert.That(legal, Has.Count.EqualTo(1));
            Assert.That(legal[0].Suit, Is.EqualTo(Suit.Hearts));
        }

        [Test]
        public void NilOnlyAvailableWhenLosingByRequiredGap()
        {
            var controller = CreateController();
            controller.StartMatch();

            controller.State.Scores[TeamId.Home].Score = 50;
            controller.State.Scores[TeamId.Away].Score = 210;
            var legal = controller.GetLegalBidsForSeat(SeatId.Bottom);
            Assert.That(legal.Contains(0), Is.True);

            controller.State.Scores[TeamId.Away].Score = 180;
            legal = controller.GetLegalBidsForSeat(SeatId.Bottom);
            Assert.That(legal.Contains(0), Is.False);
        }

        [Test]
        public void ScoreRoundTracksBagsAndThresholdPenalty()
        {
            var engine = new SpadesRuleEngine();
            var state = CreateScoringState();
            state.Scores[TeamId.Home].Bags = 9;
            state.Scores[TeamId.Home].Score = 90;
            state.Scores[TeamId.Away].Score = 40;
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 2;
            state.RoundState.TricksWonBySeat[SeatId.Bottom] = 3;
            state.RoundState.TricksWonBySeat[SeatId.Top] = 3;
            state.RoundState.TricksWonBySeat[SeatId.Left] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Right] = 3;

            var result = engine.ScoreRound(state);

            Assert.That(result.TeamScores[TeamId.Home].RoundDelta, Is.EqualTo(-49));
            Assert.That(state.Scores[TeamId.Home].Bags, Is.EqualTo(0));
        }

        [Test]
        public void StreetModeAllowsRenegeButAppliesPenalty()
        {
            var controller = new SpadesMatchController(
                RuleSetConfig.CreateStreet(100),
                new SpadesRuleEngine(),
                new Dictionary<SeatId, IAiAgent>
                {
                    { SeatId.Left, new SimpleAiAgent() },
                    { SeatId.Top, new SimpleAiAgent() },
                    { SeatId.Right, new SimpleAiAgent() }
                },
                seed: 7);
            controller.StartMatch();
            ForceBids(controller, 4, 4, 4, 4);

            var round = controller.State.RoundState;
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 8) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 6), new(Suit.Clubs, 2) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 5) };
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 7) };
            round.TrickState.CurrentTurn = SeatId.Left;
            round.TrickState.Plays.Clear();

            Assert.That(controller.TryPlayCard(SeatId.Left, round.HandsBySeat[SeatId.Left][0], out _), Is.True);
            Assert.That(controller.TryPlayCard(SeatId.Top, new Card(Suit.Clubs, 2), out _), Is.True);
            Assert.That(round.RenegeSeats.Contains(SeatId.Top), Is.True);

            var scoringState = CreateScoringState(RuleSetConfig.CreateStreet(100));
            scoringState.RoundState.RenegeSeats.Add(SeatId.Top);
            scoringState.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 4;
            scoringState.RoundState.BidState.BidsBySeat[SeatId.Top] = 0;
            scoringState.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            scoringState.RoundState.BidState.BidsBySeat[SeatId.Right] = 0;
            scoringState.RoundState.TricksWonBySeat[SeatId.Bottom] = 4;
            scoringState.RoundState.TricksWonBySeat[SeatId.Top] = 0;
            scoringState.RoundState.TricksWonBySeat[SeatId.Left] = 5;
            scoringState.RoundState.TricksWonBySeat[SeatId.Right] = 4;
            var result = new SpadesRuleEngine().ScoreRound(scoringState);

            Assert.That(result.TeamScores[TeamId.Home].RenegeDelta, Is.EqualTo(-200));
        }

        [Test]
        public void MatchCanAdvanceEndToEndWithoutSoftLock()
        {
            var controller = CreateController();
            controller.StartMatch();

            var guard = 0;
            while (controller.State.Phase != MatchPhase.MatchEnded && guard < 500)
            {
                if (controller.State.Phase == MatchPhase.Bidding)
                {
                    var seat = controller.State.RoundState.BidState.CurrentBidder;
                    var bid = controller.GetLegalBidsForSeat(seat).First();
                    Assert.That(controller.TrySubmitBid(seat, bid, out _), Is.True);
                }
                else if (controller.State.Phase == MatchPhase.TrickPlay)
                {
                    var seat = controller.State.RoundState.TrickState.CurrentTurn;
                    var card = controller.GetLegalCardsForSeat(seat).First();
                    Assert.That(controller.TryPlayCard(seat, card, out _), Is.True);
                }
                else if (controller.State.Phase == MatchPhase.RoundSummary)
                {
                    controller.StartNextRound();
                }

                guard++;
            }

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.MatchEnded));
            Assert.That(guard, Is.LessThan(500));
        }

        private static void ForceBids(SpadesMatchController controller, int bottom, int left, int top, int right)
        {
            Assert.That(controller.TrySubmitBid(SeatId.Left, left, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Top, top, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Right, right, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Bottom, bottom, out _), Is.True);
        }

        private static SpadesMatchController CreateController()
        {
            return new SpadesMatchController(
                RuleSetConfig.CreateClassic(100),
                new SpadesRuleEngine(),
                new Dictionary<SeatId, IAiAgent>
                {
                    { SeatId.Left, new SimpleAiAgent() },
                    { SeatId.Top, new SimpleAiAgent() },
                    { SeatId.Right, new SimpleAiAgent() }
                },
                seed: 42);
        }

        private static MatchState CreateScoringState(RuleSetDefinition rules = null)
        {
            var state = new MatchState
            {
                RuleSet = rules ?? RuleSetConfig.CreateClassic(100),
                RoundState = new RoundState()
            };
            state.Scores[TeamId.Home] = new ScoreSnapshot { Team = TeamId.Home };
            state.Scores[TeamId.Away] = new ScoreSnapshot { Team = TeamId.Away };
            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                state.RoundState.BidState.BidsBySeat[seat] = 0;
                state.RoundState.TricksWonBySeat[seat] = 0;
                state.RoundState.HandsBySeat[seat] = new List<Card>();
            }

            return state;
        }
    }
}
