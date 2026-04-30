using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using BackyardLegends.Runtime;
using NUnit.Framework;
using UnityEngine;

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
        public void SimpleAiAvoidsNilWithHighSpadesWhenNilIsLegal()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Hearts, 2),
                    new(Suit.Hearts, 5),
                    new(Suit.Hearts, 8),
                    new(Suit.Clubs, 3),
                    new(Suit.Clubs, 6),
                    new(Suit.Clubs, 9),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 7),
                    new(Suit.Diamonds, 9),
                    new(Suit.Spades, 3)
                });

            Assert.That(bid, Is.Not.EqualTo(0));
        }

        [Test]
        public void SimpleAiAvoidsNilWithMultipleHighCardsWhenNilIsLegal()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Hearts, 13),
                    new(Suit.Clubs, 12),
                    new(Suit.Spades, 2),
                    new(Suit.Spades, 5),
                    new(Suit.Hearts, 3),
                    new(Suit.Hearts, 6),
                    new(Suit.Clubs, 4),
                    new(Suit.Clubs, 8),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 7),
                    new(Suit.Diamonds, 9),
                    new(Suit.Spades, 8)
                });

            Assert.That(bid, Is.Not.EqualTo(0));
        }

        [Test]
        public void SimpleAiCanBidNilWithWeakHandWhenNilIsLegal()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 2),
                    new(Suit.Spades, 4),
                    new(Suit.Spades, 6),
                    new(Suit.Spades, 8),
                    new(Suit.Hearts, 2),
                    new(Suit.Hearts, 4),
                    new(Suit.Hearts, 8),
                    new(Suit.Clubs, 3),
                    new(Suit.Clubs, 6),
                    new(Suit.Clubs, 9),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 5),
                    new(Suit.Diamonds, 7)
                });

            Assert.That(bid, Is.EqualTo(0));
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
            Assert.That(result.TeamScores[TeamId.Home].BagsEarned, Is.EqualTo(1));
            Assert.That(result.TeamScores[TeamId.Home].BagPenaltyDelta, Is.EqualTo(-100));
            Assert.That(result.Summary, Does.Contain("bag penalty -100"));
            Assert.That(state.Scores[TeamId.Home].Bags, Is.EqualTo(0));
            Assert.That(state.Scores[TeamId.Home].BagsEarned, Is.EqualTo(1));
            Assert.That(state.Scores[TeamId.Home].BagPenaltyDelta, Is.EqualTo(-100));
        }

        [Test]
        public void ScoreRoundExplainsBidSixTakingEightWithBagPenalty()
        {
            var engine = new SpadesRuleEngine();
            var state = CreateScoringState();
            state.Scores[TeamId.Home].Bags = 8;
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 2;
            state.RoundState.TricksWonBySeat[SeatId.Bottom] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Top] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Left] = 3;
            state.RoundState.TricksWonBySeat[SeatId.Right] = 2;

            var result = engine.ScoreRound(state);
            var home = result.TeamScores[TeamId.Home];

            Assert.That(home.ContractBid, Is.EqualTo(6));
            Assert.That(home.TricksWon, Is.EqualTo(8));
            Assert.That(home.BagsEarned, Is.EqualTo(2));
            Assert.That(home.BagPenaltyDelta, Is.EqualTo(-100));
            Assert.That(home.RoundDelta, Is.EqualTo(-38));
            Assert.That(home.BagsAfterRound, Is.EqualTo(0));
        }

        [Test]
        public void StreetModeAllowsLeadingSpadesBeforeBroken()
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
            round.TrickState.Plays.Clear();
            round.TrickState.SpadesBroken = false;
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card>
            {
                new(Suit.Spades, 14),
                new(Suit.Hearts, 2)
            };

            var legal = controller.GetLegalCardsForSeat(SeatId.Bottom);
            Assert.That(legal, Has.Count.EqualTo(2));
            Assert.That(legal.Contains(new Card(Suit.Spades, 14)), Is.True);
        }

        [Test]
        public void StreetModeRequiresFollowingSuitBeforeCuttingWithSpades()
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
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 6), new(Suit.Spades, 2) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 5) };
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 7) };
            round.TrickState.CurrentTurn = SeatId.Left;
            round.TrickState.Plays.Clear();

            Assert.That(controller.TryPlayCard(SeatId.Left, round.HandsBySeat[SeatId.Left][0], out _), Is.True);
            var legal = controller.GetLegalCardsForSeat(SeatId.Top);
            Assert.That(legal, Has.Count.EqualTo(1));
            Assert.That(legal[0].Suit, Is.EqualTo(Suit.Hearts));
            Assert.That(controller.TryPlayCard(SeatId.Top, new Card(Suit.Spades, 2), out _), Is.False);
        }

        [Test]
        public void StreetAiFollowsSuitInsteadOfCuttingWithSpades()
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
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 6), new(Suit.Spades, 2) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 5) };
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 7) };
            round.TrickState.CurrentTurn = SeatId.Left;
            round.TrickState.Plays.Clear();

            Assert.That(controller.TryPlayCard(SeatId.Left, round.HandsBySeat[SeatId.Left][0], out _), Is.True);
            controller.AdvanceAiTurn();

            var topPlay = round.TrickState.Plays.Single(play => play.Seat == SeatId.Top);
            Assert.That(topPlay.Card.Suit, Is.EqualTo(Suit.Hearts));
            Assert.That(round.HandsBySeat[SeatId.Top].Contains(new Card(Suit.Spades, 2)), Is.True);
        }

        [Test]
        public void SimpleAiNilFollowsSuitWithLowestLosingCard()
        {
            var state = CreateScoringState();
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 8) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 6),
                    new(Suit.Hearts, 10),
                    new(Suit.Spades, 2)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 6)));
        }

        [Test]
        public void SimpleAiNilSloughsNonSpadeInsteadOfCuttingWhenVoid()
        {
            var state = CreateScoringState();
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 8) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Spades, 2),
                    new(Suit.Clubs, 3),
                    new(Suit.Diamonds, 4)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Clubs, 3)));
        }

        [Test]
        public void SimpleAiNilUsesLowestSpadeOnlyWhenNoNonSpadeOption()
        {
            var state = CreateScoringState();
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 8) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Spades, 9),
                    new(Suit.Spades, 2)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Spades, 2)));
        }

        [Test]
        public void SimpleAiNilLeadsLowestNonSpadeBeforeSpade()
        {
            var state = CreateScoringState();

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Spades, 2),
                    new(Suit.Clubs, 4),
                    new(Suit.Diamonds, 3)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Diamonds, 3)));
        }

        [Test]
        public void ThemeSpriteFactoryRebuildsDestroyedCachedRoundedRectSprite()
        {
            var fill = new Color(0.14f, 0.22f, 0.31f, 0.73f);
            var stroke = new Color(0.91f, 0.74f, 0.18f, 1f);
            var first = ThemeSpriteFactory.CreateRoundedRectSprite(fill, stroke, 47, 31, 7);

            Assert.That(first, Is.Not.Null);
            Object.DestroyImmediate(first);

            var rebuilt = ThemeSpriteFactory.CreateRoundedRectSprite(fill, stroke, 47, 31, 7);

            Assert.That(rebuilt, Is.Not.Null);
            Assert.That(rebuilt.texture, Is.Not.Null);
        }

        [Test]
        public void StreetModeScoresRenegePenalty()
        {
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
        public void SetBookDoesNotTriggerWhenTeamReachesBidBeforeRoundEnds()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 3, 4, 3, 4);
            var setBookEvents = new List<SetBookReachedEvent>();
            controller.EventRaised += matchEvent =>
            {
                if (matchEvent is SetBookReachedEvent setBook)
                {
                    setBookEvents.Add(setBook);
                }
            };

            var round = controller.State.RoundState;
            round.TricksWonBySeat[SeatId.Bottom] = 3;
            round.TricksWonBySeat[SeatId.Top] = 2;
            round.TricksWonBySeat[SeatId.Left] = 8;
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 14), new(Suit.Clubs, 2) };
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 2), new(Suit.Clubs, 3) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 13), new(Suit.Clubs, 4) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 3), new(Suit.Clubs, 5) };

            PlayCurrentTrick(controller);

            Assert.That(controller.GetTeamTricks(TeamId.Home), Is.EqualTo(6));
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.TrickPlay));
            Assert.That(setBookEvents, Is.Empty);
        }

        [Test]
        public void SetBookDoesNotTriggerWhenTeamMakesBidAtRoundEnd()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 3, 4, 3, 4);
            var setBookEvents = new List<SetBookReachedEvent>();
            controller.EventRaised += matchEvent =>
            {
                if (matchEvent is SetBookReachedEvent setBook)
                {
                    setBookEvents.Add(setBook);
                }
            };

            var round = controller.State.RoundState;
            round.TricksWonBySeat[SeatId.Bottom] = 3;
            round.TricksWonBySeat[SeatId.Top] = 2;
            round.TricksWonBySeat[SeatId.Left] = 8;
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 14) };
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 2) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 13) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 3) };

            PlayCurrentTrick(controller);

            Assert.That(controller.GetTeamTricks(TeamId.Home), Is.EqualTo(6));
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));
            Assert.That(setBookEvents, Is.Empty);
        }

        [Test]
        public void SetBookTriggersOnlyAfterRoundEndWhenTeamMissesBid()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 3, 4, 3, 4);
            var setBookEvents = new List<SetBookReachedEvent>();
            controller.EventRaised += matchEvent =>
            {
                if (matchEvent is SetBookReachedEvent setBook)
                {
                    setBookEvents.Add(setBook);
                }
            };

            var round = controller.State.RoundState;
            round.TricksWonBySeat[SeatId.Bottom] = 3;
            round.TricksWonBySeat[SeatId.Top] = 2;
            round.TricksWonBySeat[SeatId.Left] = 7;
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Left;
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 14) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 13) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 12) };
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 11) };

            PlayCurrentTrick(controller);

            Assert.That(controller.GetTeamTricks(TeamId.Home), Is.EqualTo(5));
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));
            Assert.That(setBookEvents, Has.Count.EqualTo(1));
            Assert.That(setBookEvents[0].Team, Is.EqualTo(TeamId.Home));
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

        private static int ChooseSimpleAiBid(IReadOnlyList<Card> hand)
        {
            return new SimpleAiAgent().ChooseBid(new AiBidContext
            {
                Hand = hand,
                LegalBids = Enumerable.Range(0, 14).ToList()
            });
        }

        private static Card ChooseSimpleAiCard(SeatId seat, MatchState state, IReadOnlyList<Card> legalCards)
        {
            return new SimpleAiAgent().ChooseCard(new AiPlayContext
            {
                Seat = seat,
                MatchState = state,
                Hand = legalCards,
                LegalCards = legalCards
            });
        }

        private static void ForceBids(SpadesMatchController controller, int bottom, int left, int top, int right)
        {
            Assert.That(controller.TrySubmitBid(SeatId.Left, left, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Top, top, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Right, right, out _), Is.True);
            Assert.That(controller.TrySubmitBid(SeatId.Bottom, bottom, out _), Is.True);
        }

        private static void PlayCurrentTrick(SpadesMatchController controller)
        {
            for (var i = 0; i < 4; i++)
            {
                var seat = controller.State.RoundState.TrickState.CurrentTurn;
                var card = controller.GetLegalCardsForSeat(seat).First();
                Assert.That(controller.TryPlayCard(seat, card, out _), Is.True);
            }
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
