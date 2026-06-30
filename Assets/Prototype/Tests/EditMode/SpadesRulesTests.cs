using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using BackyardLegends.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        public void SimpleAiBidsStrongSpadesAndSideSuitsCompetitively()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 9),
                    new(Suit.Spades, 8),
                    new(Suit.Spades, 7),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 13),
                    new(Suit.Hearts, 12),
                    new(Suit.Hearts, 5),
                    new(Suit.Clubs, 13),
                    new(Suit.Clubs, 12),
                    new(Suit.Diamonds, 12),
                    new(Suit.Diamonds, 4)
                });

            Assert.That(bid, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void SimpleAiValuesProtectedSideSuitHonors()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 13),
                    new(Suit.Hearts, 12),
                    new(Suit.Hearts, 11),
                    new(Suit.Hearts, 10),
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 5),
                    new(Suit.Clubs, 9),
                    new(Suit.Clubs, 8),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 3),
                    new(Suit.Diamonds, 4)
                });

            Assert.That(bid, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void SimpleAiValuesLongSpadeSuitAsTrumpBooks()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 12),
                    new(Suit.Spades, 10),
                    new(Suit.Spades, 8),
                    new(Suit.Spades, 6),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 4),
                    new(Suit.Clubs, 13),
                    new(Suit.Clubs, 5),
                    new(Suit.Diamonds, 3),
                    new(Suit.Diamonds, 7),
                    new(Suit.Diamonds, 9)
                });

            Assert.That(bid, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void SimpleAiBidsMonsterSpadeHandInDoubleDigits()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 12),
                    new(Suit.Spades, 11),
                    new(Suit.Spades, 10),
                    new(Suit.Spades, 9),
                    new(Suit.Spades, 8),
                    new(Suit.Hearts, 14),
                    new(Suit.Diamonds, 14),
                    new(Suit.Clubs, 3),
                    new(Suit.Clubs, 5),
                    new(Suit.Hearts, 4),
                    new(Suit.Diamonds, 6)
                });

            Assert.That(bid, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void SimpleAiCanBidThirteenWithNearLaydownHand()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 12),
                    new(Suit.Spades, 11),
                    new(Suit.Spades, 10),
                    new(Suit.Spades, 9),
                    new(Suit.Spades, 8),
                    new(Suit.Spades, 7),
                    new(Suit.Spades, 6),
                    new(Suit.Spades, 5),
                    new(Suit.Spades, 4),
                    new(Suit.Hearts, 14),
                    new(Suit.Diamonds, 14)
                });

            Assert.That(bid, Is.EqualTo(13));
        }

        [Test]
        public void SimpleAiBidsHighWithRunningSuitsAndTrumpControl()
        {
            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 12),
                    new(Suit.Spades, 11),
                    new(Suit.Spades, 4),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 13),
                    new(Suit.Hearts, 12),
                    new(Suit.Hearts, 11),
                    new(Suit.Clubs, 14),
                    new(Suit.Clubs, 13),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 6)
                });

            Assert.That(bid, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void SimpleAiAverageBidStaysCompetitiveAcrossSeededHands()
        {
            var random = new System.Random(12345);
            var totalBid = 0d;
            var lowBidCount = 0;
            const int handCount = 500;

            for (var i = 0; i < handCount; i++)
            {
                var deck = SpadesDeckUtility.CreateDeck();
                SpadesDeckUtility.Shuffle(deck, random);
                var bid = ChooseSimpleAiBid(
                    deck.Take(13).ToList(),
                    null,
                    SeatId.Top,
                    Enumerable.Range(1, 13).ToList());
                totalBid += bid;
                if (bid <= 2)
                {
                    lowBidCount++;
                }
            }

            Assert.That(totalBid / handCount, Is.GreaterThanOrEqualTo(3.9d));
            Assert.That(lowBidCount, Is.LessThanOrEqualTo(handCount * 0.13d));
        }

        [Test]
        public void SimpleAiControllerDealsProduceCompetitiveTableBids()
        {
            var totalAiBid = 0d;
            var lowBidCount = 0;
            var threeOrLessCount = 0;
            var aiBidCount = 0;
            const int dealCount = 80;

            for (var seed = 0; seed < dealCount; seed++)
            {
                var controller = CreateController(seed);
                controller.StartMatch();
                while (controller.State.Phase == MatchPhase.Bidding)
                {
                    if (controller.NeedsAiTurn)
                    {
                        controller.AdvanceAiTurn();
                    }
                    else
                    {
                        var legal = controller.GetLegalBidsForSeat(SeatId.Bottom);
                        var humanBid = legal.Contains(4) ? 4 : legal.First();
                        Assert.That(controller.TrySubmitBid(SeatId.Bottom, humanBid, out var error), Is.True, error);
                    }
                }

                foreach (var seat in new[] { SeatId.Left, SeatId.Top, SeatId.Right })
                {
                    var bid = controller.State.RoundState.BidState.BidsBySeat[seat];
                    Assert.That(bid.HasValue, Is.True);
                    totalAiBid += bid.Value;
                    aiBidCount++;
                    if (bid.Value <= 2)
                    {
                        lowBidCount++;
                    }

                    if (bid.Value <= 3)
                    {
                        threeOrLessCount++;
                    }
                }
            }

            Assert.That(totalAiBid / aiBidCount, Is.GreaterThanOrEqualTo(3.9d));
            Assert.That(lowBidCount, Is.LessThanOrEqualTo(aiBidCount * 0.13d));
            Assert.That(threeOrLessCount, Is.LessThanOrEqualTo(aiBidCount * 0.38d));
        }

        [Test]
        public void SimpleAiDoesNotSandbagRealBooksAfterPartnerHighBid()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 6;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = null;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = null;

            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 4),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 3),
                    new(Suit.Clubs, 2),
                    new(Suit.Clubs, 5),
                    new(Suit.Clubs, 8),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 6),
                    new(Suit.Diamonds, 9),
                    new(Suit.Diamonds, 11)
                },
                state,
                SeatId.Top);

            Assert.That(bid, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void SimpleAiSometimesStretchesUpsideHandsAboveSafeBid()
        {
            var hand = new List<Card>
            {
                new(Suit.Spades, 14),
                new(Suit.Spades, 7),
                new(Suit.Spades, 4),
                new(Suit.Hearts, 13),
                new(Suit.Hearts, 8),
                new(Suit.Hearts, 4),
                new(Suit.Clubs, 14),
                new(Suit.Clubs, 6),
                new(Suit.Clubs, 3),
                new(Suit.Diamonds, 12),
                new(Suit.Diamonds, 9),
                new(Suit.Diamonds, 5),
                new(Suit.Diamonds, 2)
            };
            var stretchedBids = 0;
            const int sampleCount = 200;

            for (var seed = 0; seed < sampleCount; seed++)
            {
                var bid = new SimpleAiAgent(seed * 17 + 3).ChooseBid(new AiBidContext
                {
                    Seat = SeatId.Top,
                    MatchState = new MatchState
                    {
                        RuleSet = RuleSetConfig.CreateClassic(100),
                        RoundState = new RoundState()
                    },
                    Hand = hand,
                    LegalBids = Enumerable.Range(1, 13).ToList()
                });
                if (bid >= 5)
                {
                    stretchedBids++;
                }
            }

            Assert.That(stretchedBids, Is.GreaterThanOrEqualTo(95));
            Assert.That(stretchedBids, Is.LessThanOrEqualTo(160));
        }

        [Test]
        public void SimpleAiRiskCanPushUpsideHandsTwoBooksHigher()
        {
            var hand = new List<Card>
            {
                new(Suit.Spades, 14),
                new(Suit.Spades, 7),
                new(Suit.Spades, 4),
                new(Suit.Hearts, 13),
                new(Suit.Hearts, 8),
                new(Suit.Hearts, 4),
                new(Suit.Clubs, 14),
                new(Suit.Clubs, 6),
                new(Suit.Clubs, 3),
                new(Suit.Diamonds, 12),
                new(Suit.Diamonds, 9),
                new(Suit.Diamonds, 5),
                new(Suit.Diamonds, 2)
            };
            const int sampleCount = 200;
            var twoBookStretches = 0;

            for (var seed = 0; seed < sampleCount; seed++)
            {
                var bid = new SimpleAiAgent(seed * 17 + 3).ChooseBid(new AiBidContext
                {
                    Seat = SeatId.Top,
                    MatchState = new MatchState
                    {
                        RuleSet = RuleSetConfig.CreateClassic(100),
                        RoundState = new RoundState()
                    },
                    Hand = hand,
                    LegalBids = Enumerable.Range(1, 13).ToList()
                });
                if (bid >= 6)
                {
                    twoBookStretches++;
                }
            }

            Assert.That(twoBookStretches, Is.GreaterThanOrEqualTo(10));
            Assert.That(twoBookStretches, Is.LessThanOrEqualTo(50));
        }

        [Test]
        public void SimpleAiBidsHigherNearBagPenaltyToReduceBags()
        {
            var state = CreateScoringState();
            state.Scores[TeamId.Home].Bags = 9;
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = null;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = null;

            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 4),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 3),
                    new(Suit.Hearts, 5),
                    new(Suit.Clubs, 2),
                    new(Suit.Clubs, 6),
                    new(Suit.Clubs, 8),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 7),
                    new(Suit.Diamonds, 9)
                },
                state,
                SeatId.Top);

            Assert.That(bid, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void SimpleAiBidsExtraToCoverPartnerNil()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 0;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = null;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = null;

            var bid = ChooseSimpleAiBid(
                new List<Card>
                {
                    new(Suit.Spades, 14),
                    new(Suit.Spades, 13),
                    new(Suit.Spades, 2),
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 3),
                    new(Suit.Clubs, 13),
                    new(Suit.Clubs, 4),
                    new(Suit.Clubs, 6),
                    new(Suit.Diamonds, 2),
                    new(Suit.Diamonds, 4),
                    new(Suit.Diamonds, 6),
                    new(Suit.Diamonds, 8),
                    new(Suit.Diamonds, 10)
                },
                state,
                SeatId.Top);

            Assert.That(bid, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void SimpleAiAvoidsDoubleNilWhenPartnerAlreadyBidNil()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 0;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = null;

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
                },
                state,
                SeatId.Top,
                Enumerable.Range(0, 14).ToList());

            Assert.That(bid, Is.Not.EqualTo(0));
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
        public void SimpleAiAvoidsOvertakingPartnerWhenContractIsCovered()
        {
            var state = CreateScoringState();
            SetTeamContractProgress(state, SeatId.Top, teamBid: 5, teamBooks: 5);
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Bottom, Card = new Card(Suit.Hearts, 13) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 2)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 2)));
        }

        [Test]
        public void SimpleAiUsesCheapestWinnerWhenTeamNeedsABook()
        {
            var state = CreateScoringState();
            SetTeamContractProgress(state, SeatId.Top, teamBid: 5, teamBooks: 4);
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 10) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 12),
                    new(Suit.Hearts, 3)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 12)));
        }

        [Test]
        public void SimpleAiDucksOpponentWinnerWhenExtraBooksWouldBecomeBags()
        {
            var state = CreateScoringState();
            SetTeamContractProgress(state, SeatId.Top, teamBid: 4, teamBooks: 5);
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 10) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 14),
                    new(Suit.Hearts, 3)
                },
                seed: 42);

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 3)));
        }

        [Test]
        public void SimpleAiLeadsPressureCardWhenTeamIsBehindContract()
        {
            var state = CreateScoringState();
            SetTeamContractProgress(state, SeatId.Top, teamBid: 7, teamBooks: 4);
            state.RoundState.TrickState.Plays.Clear();
            state.RoundState.HandsBySeat[SeatId.Top] = new List<Card>
            {
                new(Suit.Clubs, 2),
                new(Suit.Diamonds, 12),
                new(Suit.Hearts, 13),
                new(Suit.Spades, 14)
            };

            var card = ChooseSimpleAiCard(SeatId.Top, state, state.RoundState.HandsBySeat[SeatId.Top], seed: 3);

            Assert.That(card.Suit, Is.Not.EqualTo(Suit.Spades));
            Assert.That(card.Rank, Is.GreaterThanOrEqualTo(12));
        }

        [Test]
        public void SimpleAiBeatsNilOpponentCurrentWinner()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 0;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Bottom] = 2;
            state.RoundState.TricksWonBySeat[SeatId.Top] = 2;
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 9) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 10),
                    new(Suit.Hearts, 2)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 10)));
        }

        [Test]
        public void SimpleAiCoversPartnerNilWhenPartnerIsWinning()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 0;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 4;
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Bottom, Card = new Card(Suit.Hearts, 9) });
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 4) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 10),
                    new(Suit.Hearts, 2)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 10)));
        }

        [Test]
        public void SimpleAiLeadsKnownSafeWinnerWhenTeamNeedsBook()
        {
            var state = CreateScoringState();
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 3;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Bottom] = 2;
            state.RoundState.TricksWonBySeat[SeatId.Top] = 2;
            state.RoundState.TrickState.Plays.Clear();
            state.RoundState.CompletedTricks.Add(new List<TrickPlay>
            {
                new() { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 14) },
                new() { Seat = SeatId.Top, Card = new Card(Suit.Hearts, 2) },
                new() { Seat = SeatId.Right, Card = new Card(Suit.Hearts, 13) },
                new() { Seat = SeatId.Bottom, Card = new Card(Suit.Hearts, 3) }
            });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 12),
                    new(Suit.Clubs, 13),
                    new(Suit.Diamonds, 2),
                    new(Suit.Spades, 4)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 12)));
        }

        [Test]
        public void SimpleAiThrowsHighLoserWhenBagPenaltyLooms()
        {
            var state = CreateScoringState();
            state.Scores[TeamId.Home].Bags = 9;
            state.RoundState.BidState.BidsBySeat[SeatId.Bottom] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Top] = 2;
            state.RoundState.BidState.BidsBySeat[SeatId.Left] = 4;
            state.RoundState.BidState.BidsBySeat[SeatId.Right] = 4;
            state.RoundState.TricksWonBySeat[SeatId.Bottom] = 2;
            state.RoundState.TricksWonBySeat[SeatId.Top] = 2;
            state.RoundState.TrickState.LeadSuit = Suit.Hearts;
            state.RoundState.TrickState.Plays.Add(new TrickPlay { Seat = SeatId.Left, Card = new Card(Suit.Hearts, 14) });

            var card = ChooseSimpleAiCard(
                SeatId.Top,
                state,
                new List<Card>
                {
                    new(Suit.Hearts, 13),
                    new(Suit.Hearts, 3)
                });

            Assert.That(card, Is.EqualTo(new Card(Suit.Hearts, 13)));
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
        public void ClaimRemainingBooksAwardsUnplayedBooksAndScoresRound()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 3, 4, 3, 4);
            var claimedEvents = new List<RemainingBooksClaimedEvent>();
            controller.EventRaised += matchEvent =>
            {
                if (matchEvent is RemainingBooksClaimedEvent claimed)
                {
                    claimedEvents.Add(claimed);
                }
            };

            var round = controller.State.RoundState;
            round.TricksWonBySeat[SeatId.Bottom] = 2;
            round.TricksWonBySeat[SeatId.Top] = 2;
            round.TricksWonBySeat[SeatId.Left] = 3;
            round.TricksWonBySeat[SeatId.Right] = 2;
            round.TrickState.Plays.Clear();
            SetClaimHands(round, 4);

            Assert.That(controller.TryClaimRemainingBooks(TeamId.Home, out var error), Is.True, error);

            Assert.That(claimedEvents, Has.Count.EqualTo(1));
            Assert.That(claimedEvents[0].Team, Is.EqualTo(TeamId.Home));
            Assert.That(claimedEvents[0].ClaimedBooks, Is.EqualTo(4));
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));
            Assert.That(controller.GetTeamTricks(TeamId.Home), Is.EqualTo(8));
            Assert.That(controller.GetTeamTricks(TeamId.Away), Is.EqualTo(5));
            Assert.That(controller.State.Scores[TeamId.Home].RoundDelta, Is.EqualTo(62));
            Assert.That(controller.State.RoundState.HandsBySeat.Values.All(hand => hand.Count == 0), Is.True);
        }

        [Test]
        public void ClaimRemainingBooksIsOnlyAvailableDuringLiveCardPlay()
        {
            var controller = CreateController();
            controller.StartMatch();

            Assert.That(controller.TryClaimRemainingBooks(TeamId.Home, out var error), Is.False);
            Assert.That(error, Does.Contain("cards are live"));
        }

        [Test]
        public void ForfeitDuringBiddingEndsMatchWithOpponentWinnerAndPreservesScores()
        {
            var controller = CreateController();
            controller.StartMatch();
            controller.State.Scores[TeamId.Home].Score = 40;
            controller.State.Scores[TeamId.Away].Score = 20;
            var forfeitedEvents = new List<MatchForfeitedEvent>();
            var endedEvents = new List<MatchEndedEvent>();
            controller.EventRaised += matchEvent =>
            {
                if (matchEvent is MatchForfeitedEvent forfeited)
                {
                    forfeitedEvents.Add(forfeited);
                }

                if (matchEvent is MatchEndedEvent ended)
                {
                    endedEvents.Add(ended);
                }
            };

            Assert.That(controller.TryForfeitMatch(TeamId.Home, out var error), Is.True, error);

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.MatchEnded));
            Assert.That(controller.State.WinningTeam, Is.EqualTo(TeamId.Away));
            Assert.That(controller.State.Scores[TeamId.Home].Score, Is.EqualTo(40));
            Assert.That(controller.State.Scores[TeamId.Away].Score, Is.EqualTo(20));
            Assert.That(forfeitedEvents, Has.Count.EqualTo(1));
            Assert.That(forfeitedEvents[0].ForfeitingTeam, Is.EqualTo(TeamId.Home));
            Assert.That(forfeitedEvents[0].WinningTeam, Is.EqualTo(TeamId.Away));
            Assert.That(endedEvents, Has.Count.EqualTo(1));
            Assert.That(endedEvents[0].WinningTeam, Is.EqualTo(TeamId.Away));
        }

        [Test]
        public void ForfeitDuringTrickPlayEndsMatchWithOpponentWinner()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 4, 4, 4, 4);

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.TrickPlay));
            Assert.That(controller.TryForfeitMatch(TeamId.Home, out var error), Is.True, error);

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.MatchEnded));
            Assert.That(controller.State.WinningTeam, Is.EqualTo(TeamId.Away));
        }

        [Test]
        public void ForfeitIsRejectedOutsideActiveGameplay()
        {
            var controller = CreateController();

            Assert.That(controller.TryForfeitMatch(TeamId.Home, out var lobbyError), Is.False);
            Assert.That(lobbyError, Does.Contain("active match"));

            controller.StartMatch();
            Assert.That(controller.TryForfeitMatch(TeamId.Home, out _), Is.True);
            Assert.That(controller.TryForfeitMatch(TeamId.Home, out var endedError), Is.False);
            Assert.That(endedError, Does.Contain("active match"));
        }

        [Test]
        public void ForfeitIsRejectedBetweenHands()
        {
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 3, 4, 3, 4);
            var round = controller.State.RoundState;
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 14) };
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 2) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 13) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 3) };

            PlayCurrentTrick(controller);

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));
            Assert.That(controller.TryForfeitMatch(TeamId.Home, out var error), Is.False);
            Assert.That(error, Does.Contain("active match"));
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));
        }

        [Test]
        public void NewRoundKeepsHumanLastInBiddingOrder()
        {
            var controller = CreateController();
            controller.StartMatch();
            var firstDealer = controller.State.RoundState.Dealer;
            Assert.That(controller.State.RoundState.BidState.CurrentBidder, Is.EqualTo(SeatId.Left));
            ForceBids(controller, 4, 4, 4, 4);
            var round = controller.State.RoundState;
            round.TrickState.Plays.Clear();
            round.TrickState.CurrentTurn = SeatId.Bottom;
            round.HandsBySeat[SeatId.Bottom] = new List<Card> { new(Suit.Hearts, 14) };
            round.HandsBySeat[SeatId.Left] = new List<Card> { new(Suit.Hearts, 2) };
            round.HandsBySeat[SeatId.Top] = new List<Card> { new(Suit.Hearts, 13) };
            round.HandsBySeat[SeatId.Right] = new List<Card> { new(Suit.Hearts, 3) };
            PlayCurrentTrick(controller);

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.RoundSummary));

            controller.StartNextRound();

            Assert.That(controller.State.RoundState.RoundNumber, Is.EqualTo(2));
            Assert.That(controller.State.RoundState.Dealer, Is.EqualTo(firstDealer.NextClockwise()));
            Assert.That(controller.State.RoundState.BidState.CurrentBidder, Is.EqualTo(SeatId.Left));
            Assert.That(controller.TrySubmitBid(SeatId.Left, 4, out var leftError), Is.True, leftError);
            Assert.That(controller.State.RoundState.BidState.CurrentBidder, Is.EqualTo(SeatId.Top));
            Assert.That(controller.TrySubmitBid(SeatId.Top, 4, out var topError), Is.True, topError);
            Assert.That(controller.State.RoundState.BidState.CurrentBidder, Is.EqualTo(SeatId.Right));
            Assert.That(controller.State.RoundState.BidState.BidsBySeat[SeatId.Bottom], Is.Null);
            Assert.That(controller.TrySubmitBid(SeatId.Right, 4, out var rightError), Is.True, rightError);
            Assert.That(controller.State.RoundState.BidState.CurrentBidder, Is.EqualTo(SeatId.Bottom));
            Assert.That(controller.TrySubmitBid(SeatId.Bottom, 4, out var bottomError), Is.True, bottomError);
            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.TrickPlay));
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

        [Test]
        public void SimpleAiDrivenMatchCanAdvanceEndToEndWithoutSoftLock()
        {
            var controller = CreateController(seed: 1, rules: RuleSetConfig.CreateClassic(50));
            controller.StartMatch();

            var guard = 0;
            while (controller.State.Phase != MatchPhase.MatchEnded && guard < 2000)
            {
                if (controller.NeedsAiTurn)
                {
                    controller.AdvanceAiTurn();
                }
                else if (controller.State.Phase == MatchPhase.Bidding)
                {
                    var legal = controller.GetLegalBidsForSeat(SeatId.Bottom);
                    var bid = legal.Contains(4) ? 4 : legal.First();
                    Assert.That(controller.TrySubmitBid(SeatId.Bottom, bid, out var error), Is.True, error);
                }
                else if (controller.State.Phase == MatchPhase.TrickPlay)
                {
                    var legalCards = controller.GetLegalCardsForSeat(SeatId.Bottom);
                    Assert.That(controller.TryPlayCard(SeatId.Bottom, legalCards.First(), out var error), Is.True, error);
                }
                else if (controller.State.Phase == MatchPhase.RoundSummary)
                {
                    controller.StartNextRound();
                }

                guard++;
            }

            Assert.That(controller.State.Phase, Is.EqualTo(MatchPhase.MatchEnded));
            Assert.That(guard, Is.LessThan(2000));
        }

        [Test]
        public void EndOfHandScoreboardFooterUsesSelectedTargetScore()
        {
            var state = CreateScoringState(RuleSetConfig.CreateStreet(100));
            state.TargetScore = 100;
            var host = new GameObject("Scoreboard Test");
            var footerObject = new GameObject("Footer");
            footerObject.transform.SetParent(host.transform);
            var view = host.AddComponent<EndOfHandScoreboardView>();
            view.FooterText = footerObject.AddComponent<Text>();

            view.Render(state, RuleSetConfig.CreateStreet(100), false, null);

            Assert.That(view.FooterText.text, Is.EqualTo("First team to 100 wins."));
            Assert.That(view.FooterText.text, Does.Not.Contain("500"));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void EndOfHandScoreboardTogglesContinuePlayAgainAndLeaveByMatchState()
        {
            var state = CreateScoringState(RuleSetConfig.CreateStreet(100));
            var host = new GameObject("Scoreboard Toggle Test");
            var view = host.AddComponent<EndOfHandScoreboardView>();
            view.NextHandButton = CreateTestButton("Next Hand", host.transform);
            view.PlayAgainButton = CreateTestButton("Play Again", host.transform);
            view.LeaveTableButton = CreateTestButton("Leave Table", host.transform);

            view.Render(state, RuleSetConfig.CreateStreet(100), false, null);

            Assert.That(view.NextHandButton.gameObject.activeSelf, Is.True);
            Assert.That(view.NextHandButton.GetComponentInChildren<Text>().text, Is.EqualTo("Continue"));
            Assert.That(view.PlayAgainButton.gameObject.activeSelf, Is.False);
            Assert.That(view.LeaveTableButton.gameObject.activeSelf, Is.False);

            view.Render(state, RuleSetConfig.CreateStreet(100), true, TeamId.Home);

            Assert.That(view.NextHandButton.gameObject.activeSelf, Is.False);
            Assert.That(view.PlayAgainButton.gameObject.activeSelf, Is.True);
            Assert.That(view.LeaveTableButton.gameObject.activeSelf, Is.True);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void EndOfHandScoreboardPreservesContinueButtonAuthoredLayout()
        {
            var state = CreateScoringState(RuleSetConfig.CreateStreet(100));
            var host = new GameObject("Scoreboard Continue Layout Test");
            var view = host.AddComponent<EndOfHandScoreboardView>();
            view.NextHandButton = CreateTestButton("Next Hand", host.transform);
            var buttonRect = view.NextHandButton.GetComponent<RectTransform>();
            var labelRect = view.NextHandButton.GetComponentInChildren<Text>().rectTransform;
            var authoredButtonPosition = new Vector2(17.5f, -24.25f);
            var authoredButtonSize = new Vector2(188f, 52f);
            var authoredButtonScale = new Vector3(0.82f, 0.91f, 1f);
            var authoredButtonRotation = Quaternion.Euler(0f, 0f, 4.5f);
            var authoredLabelPosition = new Vector2(0f, 12f);
            var authoredLabelScale = new Vector3(1.35f, 1.2f, 1f);

            buttonRect.anchorMin = new Vector2(0.38f, 0.26f);
            buttonRect.anchorMax = new Vector2(0.62f, 0.32f);
            buttonRect.anchoredPosition = authoredButtonPosition;
            buttonRect.sizeDelta = authoredButtonSize;
            buttonRect.pivot = new Vector2(0.5f, 0.45f);
            buttonRect.localScale = authoredButtonScale;
            buttonRect.localRotation = authoredButtonRotation;
            labelRect.anchoredPosition = authoredLabelPosition;
            labelRect.localScale = authoredLabelScale;

            try
            {
                view.Render(state, RuleSetConfig.CreateStreet(100), false, null);
                buttonRect.anchoredPosition = new Vector2(999f, -999f);
                buttonRect.sizeDelta = new Vector2(10f, 10f);
                buttonRect.localScale = Vector3.one * 2f;
                buttonRect.localRotation = Quaternion.Euler(0f, 0f, -30f);
                labelRect.anchoredPosition = new Vector2(-80f, 80f);
                labelRect.localScale = Vector3.one * 0.25f;

                InvokePrivate(view, "LateUpdate");

                Assert.That(Vector2.Distance(buttonRect.anchoredPosition, authoredButtonPosition), Is.LessThan(0.001f));
                Assert.That(Vector2.Distance(buttonRect.sizeDelta, authoredButtonSize), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(buttonRect.localScale, authoredButtonScale), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(buttonRect.localRotation, authoredButtonRotation), Is.LessThan(0.001f));
                Assert.That(Vector2.Distance(labelRect.anchoredPosition, authoredLabelPosition), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(labelRect.localScale, authoredLabelScale), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EndOfHandScoreboardBindsActionsToExpectedButtons()
        {
            var host = new GameObject("Scoreboard Button Binding Test");
            var view = host.AddComponent<EndOfHandScoreboardView>();
            view.ViewHandButton = CreateTestButton("View Hand", host.transform);
            view.NextHandButton = CreateTestButton("Next Hand", host.transform);
            view.PlayAgainButton = CreateTestButton("Play Again", host.transform);
            view.LeaveTableButton = CreateTestButton("Leave Game", host.transform);
            var viewHandClicks = 0;
            var nextHandClicks = 0;
            var playAgainClicks = 0;
            var leaveClicks = 0;

            view.BindActions(
                () => viewHandClicks++,
                () => nextHandClicks++,
                () => playAgainClicks++,
                () => leaveClicks++);

            view.ViewHandButton.onClick.Invoke();
            view.NextHandButton.onClick.Invoke();
            view.PlayAgainButton.onClick.Invoke();
            view.LeaveTableButton.onClick.Invoke();

            Assert.That(viewHandClicks, Is.EqualTo(1));
            Assert.That(nextHandClicks, Is.EqualTo(1));
            Assert.That(playAgainClicks, Is.EqualTo(1));
            Assert.That(leaveClicks, Is.EqualTo(1));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void SfxTogglePersistsAndUpdatesAudioSourceMuteState()
        {
            PlayerPrefs.DeleteKey(BackyardLegendsBootstrap.SfxMutedPlayerPrefsKey);
            var host = new GameObject("SFX Toggle Test");
            var bootstrap = host.AddComponent<BackyardLegendsBootstrap>();
            var refs = host.AddComponent<BackyardLegendsSceneRefs>();
            var source = host.AddComponent<AudioSource>();
            refs.SfxToggleButton = CreateTestButton("SFX Toggle", host.transform);
            SetPrivateField(bootstrap, "sceneRefs", refs);

            try
            {
                InvokePrivate(bootstrap, "ConfigureFeedbackAudio");

                Assert.That(source.mute, Is.False);
                Assert.That(refs.SfxToggleButton.GetComponentInChildren<Text>().text, Is.EqualTo("SFX: ON"));

                InvokePrivate(bootstrap, "ToggleSfxMuted");

                Assert.That(source.mute, Is.True);
                Assert.That(PlayerPrefs.GetInt(BackyardLegendsBootstrap.SfxMutedPlayerPrefsKey), Is.EqualTo(1));
                Assert.That(refs.SfxToggleButton.GetComponentInChildren<Text>().text, Is.EqualTo("SFX: OFF"));

                InvokePrivate(bootstrap, "ToggleSfxMuted");

                Assert.That(source.mute, Is.False);
                Assert.That(PlayerPrefs.GetInt(BackyardLegendsBootstrap.SfxMutedPlayerPrefsKey), Is.EqualTo(0));
                Assert.That(refs.SfxToggleButton.GetComponentInChildren<Text>().text, Is.EqualTo("SFX: ON"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(BackyardLegendsBootstrap.SfxMutedPlayerPrefsKey);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OptionsMenuButtonStaysHiddenUntilOpeningAndBiddingAreFinished()
        {
            var host = new GameObject("Options Menu Gate Test");
            var bootstrap = host.AddComponent<BackyardLegendsBootstrap>();
            var refs = host.AddComponent<BackyardLegendsSceneRefs>();
            refs.BackButton = CreateTestButton("Menu", host.transform);
            var controller = CreateController();
            controller.StartMatch();
            SetPrivateField(bootstrap, "sceneRefs", refs);
            SetPrivateField(bootstrap, "controller", controller);

            try
            {
                SetPrivateField(bootstrap, "openingDealPending", true);
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");
                Assert.That(refs.BackButton.gameObject.activeSelf, Is.False);

                SetPrivateField(bootstrap, "openingDealPending", false);
                SetPrivateField(bootstrap, "handReviewPending", true);
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");
                Assert.That(refs.BackButton.gameObject.activeSelf, Is.False);

                SetPrivateField(bootstrap, "handReviewPending", false);
                SetPrivateField(bootstrap, "bidTurnDelayPending", true);
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");
                Assert.That(refs.BackButton.gameObject.activeSelf, Is.False);

                SetPrivateField(bootstrap, "bidTurnDelayPending", false);
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");
                Assert.That(refs.BackButton.gameObject.activeSelf, Is.False);

                ForceBids(controller, 4, 4, 4, 4);
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");

                Assert.That(refs.BackButton.gameObject.activeSelf, Is.True);
                Assert.That(refs.BackButton.interactable, Is.True);
                Assert.That(refs.BackButton.GetComponentInChildren<Text>().text, Is.EqualTo("MENU"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OptionsMenuButtonWaitsForFinalBidCalloutAfterBidding()
        {
            var host = new GameObject("Options Menu Bid Callout Gate Test");
            var bootstrap = host.AddComponent<BackyardLegendsBootstrap>();
            var refs = host.AddComponent<BackyardLegendsSceneRefs>();
            refs.BackButton = CreateTestButton("Menu", host.transform);
            var controller = CreateController();
            controller.StartMatch();
            ForceBids(controller, 4, 4, 4, 4);
            SetPrivateField(bootstrap, "sceneRefs", refs);
            SetPrivateField(bootstrap, "controller", controller);
            var bidBubbleLoops = GetPrivateField<Dictionary<SeatId, Coroutine>>(bootstrap, "bidBubbleLoops");

            try
            {
                bidBubbleLoops[SeatId.Bottom] = null;
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");
                Assert.That(refs.BackButton.gameObject.activeSelf, Is.False);

                bidBubbleLoops.Clear();
                InvokePrivate(bootstrap, "RenderOptionsMenuButton");

                Assert.That(refs.BackButton.gameObject.activeSelf, Is.True);
                Assert.That(refs.BackButton.interactable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScoreboardOpeningClearsPlayerScoreOverlayFx()
        {
            var host = new GameObject("Score Overlay Cleanup Test");
            var bootstrap = host.AddComponent<BackyardLegendsBootstrap>();
            var aura = new GameObject("Aura");
            aura.transform.SetParent(host.transform, false);
            aura.SetActive(true);
            var lightningObject = new GameObject("Lightning", typeof(RectTransform), typeof(Image));
            lightningObject.transform.SetParent(host.transform, false);
            var lightning = lightningObject.GetComponent<Image>();
            lightning.enabled = true;

            GetPrivateField<Dictionary<SeatId, GameObject>>(bootstrap, "seatAuraObjects")[SeatId.Bottom] = aura;
            GetPrivateField<Dictionary<SeatId, Component>>(bootstrap, "avatarBookLightningFx")[SeatId.Bottom] = lightning;

            try
            {
                InvokePrivate(bootstrap, "ClearPlayerScoreOverlayFx");

                Assert.That(aura.activeSelf, Is.False);
                Assert.That(lightning.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static int ChooseSimpleAiBid(IReadOnlyList<Card> hand)
        {
            return ChooseSimpleAiBid(hand, null, SeatId.Top, Enumerable.Range(0, 14).ToList());
        }

        private static int ChooseSimpleAiBid(
            IReadOnlyList<Card> hand,
            MatchState state,
            SeatId seat,
            IReadOnlyList<int> legalBids = null)
        {
            return new SimpleAiAgent().ChooseBid(new AiBidContext
            {
                Seat = seat,
                MatchState = state,
                Hand = hand,
                LegalBids = legalBids ?? Enumerable.Range(1, 13).ToList()
            });
        }

        private static Card ChooseSimpleAiCard(SeatId seat, MatchState state, IReadOnlyList<Card> legalCards, int seed = 17)
        {
            return new SimpleAiAgent(seed).ChooseCard(new AiPlayContext
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

        private static void SetTeamContractProgress(MatchState state, SeatId seat, int teamBid, int teamBooks)
        {
            var partner = seat.Partner();
            var partnerBid = teamBid > 1 ? 1 : 0;
            state.RoundState.BidState.BidsBySeat[seat] = teamBid - partnerBid;
            state.RoundState.BidState.BidsBySeat[partner] = partnerBid;
            state.RoundState.TricksWonBySeat[seat] = teamBooks;
            state.RoundState.TricksWonBySeat[partner] = 0;
            foreach (var other in SpadesSeatUtility.TurnOrder.Where(otherSeat => otherSeat.ToTeam() != seat.ToTeam()))
            {
                state.RoundState.BidState.BidsBySeat[other] = 4;
                state.RoundState.TricksWonBySeat[other] = 0;
            }
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

        private static Button CreateTestButton(string name, Transform parent)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<Text>();
            Assert.That(label, Is.Not.Null);
            label.text = name;
            var button = buttonObject.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            return button;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void SetClaimHands(RoundState round, int count)
        {
            round.HandsBySeat[SeatId.Bottom] = Enumerable.Range(2, count).Select(rank => new Card(Suit.Hearts, rank)).ToList();
            round.HandsBySeat[SeatId.Left] = Enumerable.Range(2, count).Select(rank => new Card(Suit.Clubs, rank)).ToList();
            round.HandsBySeat[SeatId.Top] = Enumerable.Range(2, count).Select(rank => new Card(Suit.Diamonds, rank)).ToList();
            round.HandsBySeat[SeatId.Right] = Enumerable.Range(2, count).Select(rank => new Card(Suit.Spades, rank)).ToList();
        }

        private static SpadesMatchController CreateController(int seed = 42, RuleSetDefinition rules = null)
        {
            return new SpadesMatchController(
                rules ?? RuleSetConfig.CreateClassic(100),
                new SpadesRuleEngine(),
                new Dictionary<SeatId, IAiAgent>
                {
                    { SeatId.Left, new SimpleAiAgent(seed * 31 + 1) },
                    { SeatId.Top, new SimpleAiAgent(seed * 31 + 2) },
                    { SeatId.Right, new SimpleAiAgent(seed * 31 + 3) }
                },
                seed: seed);
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
