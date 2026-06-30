using System.Collections.Generic;
using System.Linq;

namespace BackyardLegends.Core
{
    public interface IAiAgent
    {
        int ChooseBid(AiBidContext context);
        Card ChooseCard(AiPlayContext context);
    }

    public sealed class AiBidContext
    {
        public SeatId Seat;
        public MatchState MatchState;
        public IReadOnlyList<Card> Hand;
        public IReadOnlyList<int> LegalBids;
    }

    public sealed class AiPlayContext
    {
        public SeatId Seat;
        public MatchState MatchState;
        public IReadOnlyList<Card> Hand;
        public IReadOnlyList<Card> LegalCards;
    }

    public sealed class SimpleAiAgent : IAiAgent
    {
        private static readonly object SeedLock = new();
        private static readonly System.Random SeedSource = new();
        private readonly System.Random random;

        public SimpleAiAgent() : this(NextSeed())
        {
        }

        public SimpleAiAgent(int seed)
        {
            random = new System.Random(seed);
        }

        public int ChooseBid(AiBidContext context)
        {
            var estimatedTricks = EstimateTricks(context.Hand);
            if (context.LegalBids.Contains(0) &&
                !PartnerBidIsNil(context) &&
                IsNilCandidate(context.Hand, estimatedTricks))
            {
                return 0;
            }

            var adjustedTricks = ApplyRiskyBidStretch(context, AdjustBidForContext(context, estimatedTricks));
            var closest = context.LegalBids
                .Where(bid => bid > 0)
                .OrderBy(bid => System.Math.Abs(bid - adjustedTricks))
                .ThenByDescending(bid => bid)
                .First();
            return closest;
        }

        public Card ChooseCard(AiPlayContext context)
        {
            var legalCards = context.LegalCards.ToList();
            var plan = BuildPlayPlan(context);
            if (IsNilBid(context))
            {
                return ChooseNilCard(context, legalCards);
            }

            var trick = context.MatchState.RoundState.TrickState;
            if (trick.Plays.Count == 0)
            {
                return ChooseLeadCard(legalCards, plan, random);
            }

            var currentWinningSeat = new SpadesRuleEngine().ResolveTrickWinner(trick);
            var currentWinningCard = trick.Plays.First(play => play.Seat == currentWinningSeat).Card;
            var leadSuit = trick.LeadSuit ?? trick.Plays[0].Card.Suit;
            var partnerWinning = currentWinningSeat == context.Seat.Partner();

            var winningCards = legalCards
                .Where(card => Beats(card, currentWinningCard, leadSuit))
                .OrderBy(card => WinningCost(card, leadSuit))
                .ThenBy(card => card.Rank)
                .ToList();

            if (plan.OpponentNilSeats.Contains(currentWinningSeat) && winningCards.Count > 0)
            {
                return ChooseWinningCard(winningCards, plan, random);
            }

            if (partnerWinning && plan.PartnerBidNil && winningCards.Count > 0)
            {
                return ChooseWinningCard(winningCards, plan, random);
            }

            if (partnerWinning && !plan.NeedsBookNow)
            {
                return ChooseDiscardCard(legalCards, leadSuit, currentWinningCard, true, plan, random);
            }

            var shouldTryToWin = plan.NeedsBookNow || (!partnerWinning && ShouldContestTrick(plan, trick.Plays.Count, random));
            if (shouldTryToWin && winningCards.Count > 0)
            {
                return ChooseWinningCard(winningCards, plan, random);
            }

            return ChooseDiscardCard(legalCards, leadSuit, currentWinningCard, false, plan, random);
        }

        private static Card ChooseNilCard(AiPlayContext context, List<Card> legalCards)
        {
            var trick = context.MatchState.RoundState.TrickState;
            if (trick.Plays.Count == 0)
            {
                return LowestNonSpadeFirst(legalCards).First();
            }

            var currentWinningSeat = new SpadesRuleEngine().ResolveTrickWinner(trick);
            var currentWinningCard = trick.Plays.First(play => play.Seat == currentWinningSeat).Card;
            var leadSuit = trick.LeadSuit ?? trick.Plays[0].Card.Suit;
            var followSuitCards = legalCards.Where(card => card.Suit == leadSuit).ToList();
            var candidates = followSuitCards.Count > 0
                ? followSuitCards
                : legalCards.Where(card => card.Suit != Suit.Spades).ToList();

            if (candidates.Count == 0)
            {
                candidates = legalCards;
            }

            var losingCards = candidates
                .Where(card => !Beats(card, currentWinningCard, leadSuit))
                .OrderBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();

            if (losingCards.Count > 0)
            {
                return losingCards.First();
            }

            return candidates
                .OrderBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .First();
        }

        private static int EstimateTricks(IEnumerable<Card> hand)
        {
            var cards = hand.ToList();
            if (cards.Count == 0)
            {
                return 1;
            }

            var expectedBooks = EstimateSpadeTricks(cards.Where(card => card.Suit == Suit.Spades).ToList());

            foreach (var suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts })
            {
                expectedBooks += EstimateSideSuitTricks(cards.Where(card => card.Suit == suit).ToList());
            }

            expectedBooks += EstimateShortSuitTrumpTricks(cards);

            var bid = (int)System.Math.Floor(expectedBooks + 1d);
            if (HasCompetitiveBidShape(cards) && bid < 3)
            {
                bid = 3;
            }

            if (HasStrongBidShape(cards) && bid < 4)
            {
                bid = 4;
            }

            bid = ApplyHighBidTiers(cards, bid);

            return System.Math.Clamp(bid, 1, 13);
        }

        private static double EstimateSpadeTricks(IReadOnlyCollection<Card> spades)
        {
            if (spades.Count == 0)
            {
                return 0d;
            }

            var expectedBooks = 0d;
            if (HasRank(spades, 14))
            {
                expectedBooks += 1.05d;
            }

            if (HasRank(spades, 13))
            {
                expectedBooks += 1d;
            }

            if (HasRank(spades, 12))
            {
                expectedBooks += 0.9d;
            }

            if (HasRank(spades, 11))
            {
                expectedBooks += 0.7d;
            }

            if (HasRank(spades, 10))
            {
                expectedBooks += 0.45d;
            }

            if (HasRank(spades, 9))
            {
                expectedBooks += 0.25d;
            }

            if (HasRank(spades, 8))
            {
                expectedBooks += 0.1d;
            }

            if (spades.Count > 3)
            {
                expectedBooks += (spades.Count - 3) * 0.55d;
            }

            if (spades.Count > 5)
            {
                expectedBooks += (spades.Count - 5) * 0.2d;
            }

            return expectedBooks;
        }

        private static double EstimateSideSuitTricks(IReadOnlyCollection<Card> suitCards)
        {
            if (suitCards.Count == 0)
            {
                return 0d;
            }

            var hasAce = HasRank(suitCards, 14);
            var hasKing = HasRank(suitCards, 13);
            var hasQueen = HasRank(suitCards, 12);
            var hasJack = HasRank(suitCards, 11);
            var expectedBooks = 0d;

            if (hasAce)
            {
                expectedBooks += 1d;
            }

            if (hasKing)
            {
                expectedBooks += hasAce ? 0.9d : 0.75d;
            }

            if (hasQueen)
            {
                if (hasAce && hasKing)
                {
                    expectedBooks += 0.7d;
                }
                else if (hasKing)
                {
                    expectedBooks += 0.5d;
                }
                else if (hasAce && suitCards.Count <= 4)
                {
                    expectedBooks += 0.32d;
                }
                else
                {
                    expectedBooks += 0.2d;
                }
            }

            if (hasJack)
            {
                if (hasAce && hasKing && hasQueen)
                {
                    expectedBooks += 0.32d;
                }
                else if (hasKing && hasQueen && suitCards.Count <= 4)
                {
                    expectedBooks += 0.24d;
                }
                else if (hasAce && hasQueen && suitCards.Count <= 4)
                {
                    expectedBooks += 0.12d;
                }
            }

            if (suitCards.Count >= 5 && (hasAce || hasKing))
            {
                expectedBooks += 0.3d;
            }

            return expectedBooks;
        }

        private static double EstimateShortSuitTrumpTricks(IReadOnlyCollection<Card> hand)
        {
            var spadeCount = hand.Count(card => card.Suit == Suit.Spades);
            if (spadeCount < 3)
            {
                return 0d;
            }

            var expectedBooks = 0d;
            foreach (var suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts })
            {
                var suitLength = hand.Count(card => card.Suit == suit);
                if (suitLength == 0)
                {
                    expectedBooks += spadeCount >= 4 ? 0.85d : 0.45d;
                }
                else if (suitLength == 1)
                {
                    expectedBooks += spadeCount >= 4 ? 0.52d : 0.3d;
                }
                else if (suitLength == 2 && spadeCount >= 5)
                {
                    expectedBooks += 0.3d;
                }
            }

            return expectedBooks;
        }

        private static bool HasCompetitiveBidShape(IReadOnlyCollection<Card> cards)
        {
            var highSpades = cards.Count(card => card.Suit == Suit.Spades && card.Rank >= 10);
            var spadeCount = cards.Count(card => card.Suit == Suit.Spades);
            var highCards = cards.Count(card => card.Rank >= 12);
            return cards.Any(card => card.Rank == 14) ||
                   highSpades >= 2 ||
                   spadeCount >= 5 ||
                   highCards >= 4;
        }

        private static bool HasStrongBidShape(IReadOnlyCollection<Card> cards)
        {
            var aces = cards.Count(card => card.Rank == 14);
            var highSpades = cards.Count(card => card.Suit == Suit.Spades && card.Rank >= 10);
            var spadeCount = cards.Count(card => card.Suit == Suit.Spades);
            var highCards = cards.Count(card => card.Rank >= 12);
            return aces >= 2 ||
                   highSpades >= 2 ||
                   spadeCount >= 5 ||
                   highCards >= 5 ||
                   spadeCount >= 4 && highCards >= 3;
        }

        private static int ApplyHighBidTiers(IReadOnlyCollection<Card> cards, int bid)
        {
            var spades = cards.Where(card => card.Suit == Suit.Spades).ToList();
            var spadeCount = spades.Count;
            var highSpades = spades.Count(card => card.Rank >= 10);
            var topSpades = spades.Count(card => card.Rank >= 11);
            var topSpadeRun = CountTopRun(spades);
            var aces = cards.Count(card => card.Rank == 14);
            var sideAces = cards.Count(card => card.Suit != Suit.Spades && card.Rank == 14);
            var highCards = cards.Count(card => card.Rank >= 12);
            var runningSuitBooks = EstimateRunningSuitBooks(cards);
            var voidCount = CountSideSuitsWithLength(cards, 0);
            var singletonCount = CountSideSuitsWithLength(cards, 1);

            if (aces >= 3 || highSpades >= 3 || spadeCount >= 6 && highSpades >= 1 || runningSuitBooks >= 4)
            {
                bid = System.Math.Max(bid, 5);
            }

            if (topSpades >= 3 ||
                spadeCount >= 5 && highSpades >= 3 ||
                aces >= 3 && highCards >= 5 ||
                runningSuitBooks >= 5)
            {
                bid = System.Math.Max(bid, 6);
            }

            if (spadeCount >= 6 && highSpades >= 3 ||
                topSpadeRun >= 3 && spadeCount >= 5 && sideAces >= 1 ||
                runningSuitBooks >= 6 ||
                spadeCount >= 5 && highSpades >= 2 && voidCount >= 1)
            {
                bid = System.Math.Max(bid, 7);
            }

            if (topSpadeRun >= 4 && spadeCount >= 5 ||
                spadeCount >= 7 && highSpades >= 4 ||
                spadeCount >= 6 && highSpades >= 3 && aces >= 2 ||
                runningSuitBooks >= 7 && spadeCount >= 4)
            {
                bid = System.Math.Max(bid, 8);
            }

            if (topSpadeRun >= 4 && spadeCount >= 6 && sideAces >= 2 ||
                spadeCount >= 7 && highSpades >= 4 && aces >= 2 ||
                runningSuitBooks >= 8 && spadeCount >= 5 ||
                topSpadeRun >= 3 && spadeCount >= 7 && voidCount + singletonCount >= 2)
            {
                bid = System.Math.Max(bid, 9);
            }

            if (topSpadeRun >= 5 && spadeCount >= 7 && sideAces >= 2 ||
                spadeCount >= 8 && highSpades >= 5 && aces >= 3 ||
                runningSuitBooks >= 9 && topSpadeRun >= 4)
            {
                bid = System.Math.Max(bid, 10);
            }

            if (spadeCount >= 9 && topSpadeRun >= 5 && sideAces >= 2 ||
                runningSuitBooks >= 10 && spadeCount >= 6 && highSpades >= 4)
            {
                bid = System.Math.Max(bid, 11);
            }

            if (spadeCount >= 10 && topSpadeRun >= 5 && sideAces >= 2)
            {
                bid = System.Math.Max(bid, 12);
            }

            if (spadeCount >= 11 && topSpadeRun >= 5 && sideAces >= 2)
            {
                bid = 13;
            }

            return bid;
        }

        private static int CountTopRun(IEnumerable<Card> cards)
        {
            var run = 0;
            for (var rank = 14; rank >= 10; rank--)
            {
                if (!HasRank(cards, rank))
                {
                    break;
                }

                run++;
            }

            return run;
        }

        private static int EstimateRunningSuitBooks(IReadOnlyCollection<Card> cards)
        {
            var books = 0;
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                var suitCards = cards.Where(card => card.Suit == suit).ToList();
                var run = CountTopRun(suitCards);
                if (suit == Suit.Spades)
                {
                    books += run;
                    if (suitCards.Count >= 6 && run >= 3)
                    {
                        books += 1;
                    }
                }
                else
                {
                    books += System.Math.Max(0, run - (suitCards.Count >= 5 ? 1 : 0));
                }
            }

            return books;
        }

        private static int CountSideSuitsWithLength(IReadOnlyCollection<Card> cards, int length)
        {
            var count = 0;
            foreach (var suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts })
            {
                if (cards.Count(card => card.Suit == suit) == length)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasRank(IEnumerable<Card> cards, int rank)
        {
            return cards.Any(card => card.Rank == rank);
        }

        private static int AdjustBidForContext(AiBidContext context, int estimatedTricks)
        {
            if (context.MatchState?.RoundState == null)
            {
                return estimatedTricks;
            }

            var adjusted = estimatedTricks;
            var state = context.MatchState;
            var hand = context.Hand?.ToList() ?? new List<Card>();
            var partnerBid = GetBid(state, context.Seat.Partner());
            var bagDanger = IsNearBagPenalty(state, context.Seat.ToTeam());

            if (partnerBid.HasValue)
            {
                if (partnerBid.Value == 0)
                {
                    adjusted += HasNilCoverStrength(hand) ? 1 : 0;
                }
                else if (partnerBid.Value <= 1 && adjusted >= 4)
                {
                    adjusted += 1;
                }
            }

            if (bagDanger && adjusted >= 2)
            {
                adjusted += 1;
            }

            if (IsLastBidder(context) && adjusted >= 3 && !bagDanger)
            {
                var knownTableBid = state.RoundState.BidState.BidsBySeat
                    .Where(entry => entry.Key != context.Seat && entry.Value.HasValue)
                    .Sum(entry => entry.Value ?? 0);
                if (knownTableBid + adjusted <= 10)
                {
                    adjusted += 1;
                }
            }

            var maxBid = state.RuleSet?.MaxBid ?? 13;
            return System.Math.Clamp(adjusted, 1, maxBid);
        }

        private int ApplyRiskyBidStretch(AiBidContext context, int bid)
        {
            var cards = context.Hand?.ToList() ?? new List<Card>();
            if (cards.Count == 0)
            {
                return bid;
            }

            var maxBid = context.MatchState?.RuleSet?.MaxBid ?? 13;
            if (bid >= maxBid)
            {
                return bid;
            }

            var stretchChance = GetBidStretchChance(context, cards, bid);
            if (random.NextDouble() >= stretchChance)
            {
                return bid;
            }

            bid += 1;
            if (bid >= maxBid)
            {
                return bid;
            }

            var doubleStretchChance = GetDoubleStretchChance(cards, bid, stretchChance);
            if (random.NextDouble() < doubleStretchChance)
            {
                bid += 1;
            }

            return System.Math.Min(bid, maxBid);
        }

        private static double GetBidStretchChance(AiBidContext context, IReadOnlyCollection<Card> cards, int bid)
        {
            var speculativeShape = HasSpeculativeBidShape(cards);
            var chance = 0.16d;
            if (HasCompetitiveBidShape(cards))
            {
                chance += 0.16d;
            }

            if (HasStrongBidShape(cards))
            {
                chance += 0.18d;
            }

            if (HasBidUpside(cards))
            {
                chance += 0.14d;
            }

            if (speculativeShape)
            {
                chance += 0.08d;
            }

            if (bid <= 2)
            {
                chance *= speculativeShape ? 0.75d : 0.65d;
            }
            else if (bid >= 6)
            {
                chance *= 0.8d;
            }

            var state = context.MatchState;
            if (state?.RoundState != null)
            {
                var partnerBid = GetBid(state, context.Seat.Partner());
                if (partnerBid.HasValue && partnerBid.Value <= 1 && bid >= 3)
                {
                    chance += 0.08d;
                }

                if (IsNearBagPenalty(state, context.Seat.ToTeam()) && bid >= 2)
                {
                    chance += 0.1d;
                }

                if (IsLastBidder(context))
                {
                    var knownTableBid = state.RoundState.BidState.BidsBySeat
                        .Where(entry => entry.Key != context.Seat && entry.Value.HasValue)
                        .Sum(entry => entry.Value ?? 0);
                    if (knownTableBid + bid <= 12)
                    {
                        chance += 0.16d;
                    }
                }
            }

            return System.Math.Clamp(chance, 0.08d, 0.74d);
        }

        private static double GetDoubleStretchChance(IReadOnlyCollection<Card> cards, int bid, double stretchChance)
        {
            var hasStrongShape = HasStrongBidShape(cards);
            var hasUpside = HasBidUpside(cards);
            if (bid < 4 || !hasStrongShape && !hasUpside)
            {
                return 0d;
            }

            var chance = hasStrongShape ? 0.08d : 0.03d;
            if (hasUpside)
            {
                chance += 0.1d;
            }

            if (bid <= 6)
            {
                chance += 0.06d;
            }

            return System.Math.Min(chance, stretchChance * 0.5d);
        }

        private static bool HasBidUpside(IReadOnlyCollection<Card> cards)
        {
            var spadeCount = cards.Count(card => card.Suit == Suit.Spades);
            var highSpades = cards.Count(card => card.Suit == Suit.Spades && card.Rank >= 9);
            var aces = cards.Count(card => card.Rank == 14);
            var highCards = cards.Count(card => card.Rank >= 11);
            var voidCount = CountSideSuitsWithLength(cards, 0);
            var singletonCount = CountSideSuitsWithLength(cards, 1);
            return spadeCount >= 4 && highSpades >= 2 ||
                   spadeCount >= 5 ||
                   aces >= 2 ||
                   highCards >= 5 ||
                   voidCount + singletonCount >= 2 && spadeCount >= 3;
        }

        private static bool HasSpeculativeBidShape(IReadOnlyCollection<Card> cards)
        {
            var spadeCount = cards.Count(card => card.Suit == Suit.Spades);
            var mediumSpades = cards.Count(card => card.Suit == Suit.Spades && card.Rank >= 8);
            var broadways = cards.Count(card => card.Rank >= 11);
            var sideKings = cards.Count(card => card.Suit != Suit.Spades && card.Rank == 13);
            var singletonCount = CountSideSuitsWithLength(cards, 1);
            return spadeCount >= 3 && mediumSpades >= 1 ||
                   spadeCount >= 2 && broadways >= 3 ||
                   sideKings >= 2 ||
                   singletonCount >= 1 && spadeCount >= 3;
        }

        private static bool PartnerBidIsNil(AiBidContext context)
        {
            return GetBid(context.MatchState, context.Seat.Partner()) == 0;
        }

        private static bool HasNilCoverStrength(IReadOnlyCollection<Card> hand)
        {
            var highSpades = hand.Count(card => card.Suit == Suit.Spades && card.Rank >= 11);
            var highCards = hand.Count(card => card.Rank >= 13);
            return highSpades >= 2 || highCards >= 3 || hand.Any(card => card.Suit == Suit.Spades && card.Rank == 14);
        }

        private static bool IsLastBidder(AiBidContext context)
        {
            if (context.MatchState?.RoundState == null)
            {
                return false;
            }

            var submittedBids = context.MatchState.RoundState.BidState.BidsBySeat
                .Count(entry => entry.Key != context.Seat && entry.Value.HasValue);
            return submittedBids >= 3;
        }

        private static int? GetBid(MatchState state, SeatId seat)
        {
            if (state?.RoundState == null)
            {
                return null;
            }

            return state.RoundState.BidState.BidsBySeat.TryGetValue(seat, out var bid) ? bid : null;
        }

        private static bool IsNearBagPenalty(MatchState state, TeamId team)
        {
            if (state == null || !state.Scores.TryGetValue(team, out var score))
            {
                return false;
            }

            var threshold = state.RuleSet?.BagPenaltyThreshold ?? 10;
            return threshold > 0 && score.Bags >= System.Math.Max(0, threshold - 2);
        }

        private static bool IsNilCandidate(IEnumerable<Card> hand, int estimatedTricks)
        {
            var cards = hand.ToList();
            if (estimatedTricks > 1)
            {
                return false;
            }

            if (cards.Any(card => card.Suit == Suit.Spades && card.Rank >= 12))
            {
                return false;
            }

            if (cards.Any(card => card.Rank == 14))
            {
                return false;
            }

            if (cards.Count(card => card.Rank >= 12) >= 2)
            {
                return false;
            }

            if (cards.Count(card => card.Suit == Suit.Spades && card.Rank >= 10) >= 2)
            {
                return false;
            }

            return true;
        }

        private AiPlayPlan BuildPlayPlan(AiPlayContext context)
        {
            var round = context.MatchState.RoundState;
            var team = context.Seat.ToTeam();
            var partner = context.Seat.Partner();
            var teamBid = round.BidState.BidsBySeat
                .Where(entry => entry.Key.ToTeam() == team && entry.Value.HasValue)
                .Sum(entry => entry.Value ?? 0);
            var teamBooks = round.TricksWonBySeat
                .Where(entry => entry.Key.ToTeam() == team)
                .Sum(entry => entry.Value);
            var booksRemaining = round.HandsBySeat.TryGetValue(context.Seat, out var hand) && hand.Count > 0
                ? hand.Count
                : context.Hand?.Count ?? 0;
            var booksNeeded = System.Math.Max(0, teamBid - teamBooks);
            var pressure = booksNeeded > 0 && booksNeeded >= System.Math.Max(1, booksRemaining - 1);
            var opponentNilSeats = round.BidState.BidsBySeat
                .Where(entry => entry.Key.ToTeam() != team && entry.Value == 0)
                .Select(entry => entry.Key)
                .ToList();
            var partnerBidNil = round.BidState.BidsBySeat.TryGetValue(partner, out var partnerBid) && partnerBid == 0;
            return new AiPlayPlan(
                teamBid,
                teamBooks,
                booksNeeded,
                booksRemaining,
                pressure,
                IsNearBagPenalty(context.MatchState, team),
                partnerBidNil,
                opponentNilSeats,
                BuildKnownCards(context));
        }

        private static IReadOnlyCollection<Card> BuildKnownCards(AiPlayContext context)
        {
            var knownCards = new List<Card>();
            if (context.Hand != null)
            {
                knownCards.AddRange(context.Hand);
            }

            var round = context.MatchState?.RoundState;
            if (round == null)
            {
                return knownCards;
            }

            foreach (var play in round.TrickState.Plays)
            {
                knownCards.Add(play.Card);
            }

            foreach (var trick in round.CompletedTricks)
            {
                knownCards.AddRange(trick.Select(play => play.Card));
            }

            return knownCards;
        }

        private static Card ChooseLeadCard(List<Card> legalCards, AiPlayPlan plan, System.Random random)
        {
            var nonSpades = legalCards.Where(card => card.Suit != Suit.Spades).ToList();
            var leadPool = nonSpades.Count > 0 ? nonSpades : legalCards;
            if (plan.PartnerBidNil)
            {
                return ChooseNilCoverLead(leadPool);
            }

            if (plan.OpponentNilSeats.Count > 0 && !plan.UnderPressure)
            {
                return ChooseNilAttackLead(leadPool);
            }

            if (plan.NeedsBookNow)
            {
                var knownWinners = leadPool
                    .Where(card => IsKnownWinner(card, plan.KnownCards))
                    .OrderBy(card => card.Suit == Suit.Spades)
                    .ThenBy(card => card.Rank)
                    .ThenBy(card => card.Suit)
                    .ToList();
                if (knownWinners.Count > 0)
                {
                    return knownWinners.First();
                }

                var pressureCards = leadPool
                    .Where(card => card.Rank >= (plan.UnderPressure ? 11 : 12))
                    .OrderByDescending(card => card.Rank)
                    .ThenBy(card => card.Suit == Suit.Spades)
                    .ToList();
                if (pressureCards.Count > 0 && (plan.UnderPressure || random.NextDouble() < 0.68))
                {
                    return ChooseFromWindow(pressureCards, 2, random);
                }
            }

            if (plan.BagDanger && !plan.NeedsBookNow)
            {
                var bagSafeLeads = leadPool
                    .OrderBy(card => IsKnownWinner(card, plan.KnownCards))
                    .ThenBy(card => LeadRisk(card, legalCards))
                    .ThenBy(card => card.Rank)
                    .ThenBy(card => card.Suit)
                    .ToList();
                return ChooseFromWindow(bagSafeLeads, 2, random);
            }

            var safeLeads = leadPool
                .OrderBy(card => LeadRisk(card, legalCards))
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();
            return ChooseFromWindow(safeLeads, plan.NeedsBookNow ? 3 : 2, random);
        }

        private static Card ChooseNilCoverLead(IEnumerable<Card> leadPool)
        {
            var cards = leadPool.ToList();
            var coverCards = cards
                .Where(card => card.Suit != Suit.Spades && card.Rank >= 10)
                .OrderByDescending(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();
            if (coverCards.Count > 0)
            {
                return coverCards.First();
            }

            return LowestNonSpadeFirst(cards).First();
        }

        private static Card ChooseNilAttackLead(IEnumerable<Card> leadPool)
        {
            var cards = leadPool.ToList();
            var attackCards = cards
                .Where(card => card.Suit != Suit.Spades)
                .OrderByDescending(card => cards.Count(held => held.Suit == card.Suit))
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();
            return attackCards.Count > 0
                ? attackCards.First()
                : cards.OrderBy(card => card.Rank).ThenBy(card => card.Suit).First();
        }

        private static Card ChooseWinningCard(List<Card> winningCards, AiPlayPlan plan, System.Random random)
        {
            var ordered = winningCards
                .OrderBy(card => WinningCost(card, null))
                .ThenBy(card => card.Rank)
                .ToList();

            if (plan.UnderPressure && ordered.Count > 1 && random.NextDouble() < 0.28)
            {
                return ordered[1];
            }

            return ordered.First();
        }

        private static Card ChooseDiscardCard(
            List<Card> legalCards,
            Suit leadSuit,
            Card currentWinningCard,
            bool partnerWinning,
            AiPlayPlan plan,
            System.Random random)
        {
            var losingCards = legalCards
                .Where(card => !Beats(card, currentWinningCard, leadSuit))
                .ToList();

            if (losingCards.Count > 0)
            {
                if (plan.BagDanger && !plan.NeedsBookNow)
                {
                    return losingCards
                        .OrderBy(card => card.Suit == Suit.Spades)
                        .ThenByDescending(card => card.Rank)
                        .ThenBy(card => card.Suit)
                        .First();
                }

                losingCards = losingCards
                    .OrderBy(card => DiscardCost(card, partnerWinning))
                    .ThenBy(card => card.Rank)
                    .ThenBy(card => card.Suit)
                    .ToList();
                return ChooseFromWindow(losingCards, 3, random);
            }

            return legalCards
                .OrderBy(card => WinningCost(card, leadSuit))
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .First();
        }

        private static bool ShouldContestTrick(AiPlayPlan plan, int playsInTrick, System.Random random)
        {
            if (plan.BagDanger && plan.TeamBooks >= plan.TeamBid)
            {
                return false;
            }

            if (plan.TeamBooks < plan.TeamBid)
            {
                return random.NextDouble() < (playsInTrick >= 2 ? 0.72 : 0.55);
            }

            if (plan.TeamBid == 0)
            {
                return false;
            }

            return random.NextDouble() < 0.24;
        }

        private static Card ChooseFromWindow(IReadOnlyList<Card> orderedCards, int windowSize, System.Random random)
        {
            if (orderedCards.Count == 0)
            {
                return default;
            }

            var count = System.Math.Min(windowSize, orderedCards.Count);
            return orderedCards[random.Next(count)];
        }

        private static int LeadRisk(Card card, List<Card> hand)
        {
            var suitLength = hand.Count(held => held.Suit == card.Suit);
            var shortSuitBonus = suitLength <= 2 ? -2 : 0;
            var spadePenalty = card.Suit == Suit.Spades ? 8 : 0;
            var highCardPenalty = card.Rank >= 12 ? 5 : card.Rank >= 10 ? 2 : 0;
            return card.Rank + highCardPenalty + spadePenalty + shortSuitBonus;
        }

        private static bool IsKnownWinner(Card card, IReadOnlyCollection<Card> knownCards)
        {
            for (var rank = card.Rank + 1; rank <= 14; rank++)
            {
                if (!knownCards.Contains(new Card(card.Suit, rank)))
                {
                    return false;
                }
            }

            return true;
        }

        private static int WinningCost(Card card, Suit? leadSuit)
        {
            var spadeCost = card.Suit == Suit.Spades ? 24 : 0;
            var offSuitCost = leadSuit.HasValue && card.Suit != leadSuit.Value ? 8 : 0;
            var highCardCost = card.Rank >= 13 ? 4 : 0;
            return card.Rank + spadeCost + offSuitCost + highCardCost;
        }

        private static int DiscardCost(Card card, bool partnerWinning)
        {
            var spadePenalty = card.Suit == Suit.Spades ? 20 : 0;
            var highCardPenalty = card.Rank >= 12 && partnerWinning ? 6 : 0;
            return card.Rank + spadePenalty + highCardPenalty;
        }

        private static int NextSeed()
        {
            lock (SeedLock)
            {
                return SeedSource.Next();
            }
        }

        private static bool IsNilBid(AiPlayContext context)
        {
            return context.MatchState.RoundState.BidState.BidsBySeat.TryGetValue(context.Seat, out var bid) &&
                   bid == 0;
        }

        private static IOrderedEnumerable<Card> LowestNonSpadeFirst(IEnumerable<Card> cards)
        {
            return cards
                .OrderBy(card => card.Suit == Suit.Spades)
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit);
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

        private readonly struct AiPlayPlan
        {
            public AiPlayPlan(
                int teamBid,
                int teamBooks,
                int booksNeeded,
                int booksRemaining,
                bool underPressure,
                bool bagDanger,
                bool partnerBidNil,
                IReadOnlyList<SeatId> opponentNilSeats,
                IReadOnlyCollection<Card> knownCards)
            {
                TeamBid = teamBid;
                TeamBooks = teamBooks;
                BooksNeeded = booksNeeded;
                BooksRemaining = booksRemaining;
                UnderPressure = underPressure;
                BagDanger = bagDanger;
                PartnerBidNil = partnerBidNil;
                OpponentNilSeats = opponentNilSeats;
                KnownCards = knownCards;
            }

            public int TeamBid { get; }
            public int TeamBooks { get; }
            public int BooksNeeded { get; }
            public int BooksRemaining { get; }
            public bool NeedsBookNow => BooksNeeded > 0;
            public bool UnderPressure { get; }
            public bool BagDanger { get; }
            public bool PartnerBidNil { get; }
            public IReadOnlyList<SeatId> OpponentNilSeats { get; }
            public IReadOnlyCollection<Card> KnownCards { get; }
        }
    }
}
