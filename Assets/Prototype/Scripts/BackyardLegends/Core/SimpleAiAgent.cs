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
            if (context.LegalBids.Contains(0) && IsNilCandidate(context.Hand, estimatedTricks))
            {
                return 0;
            }

            var closest = context.LegalBids
                .OrderBy(bid => System.Math.Abs(bid - estimatedTricks))
                .ThenByDescending(bid => bid)
                .First();
            return closest;
        }

        public Card ChooseCard(AiPlayContext context)
        {
            var legalCards = context.LegalCards.ToList();
            if (IsNilBid(context))
            {
                return ChooseNilCard(context, legalCards);
            }

            var trick = context.MatchState.RoundState.TrickState;
            var plan = BuildPlayPlan(context);
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

            if (partnerWinning && !plan.NeedsBookNow)
            {
                return ChooseDiscardCard(legalCards, leadSuit, currentWinningCard, true, random);
            }

            var shouldTryToWin = plan.NeedsBookNow || (!partnerWinning && ShouldContestTrick(plan, trick.Plays.Count, random));
            if (shouldTryToWin && winningCards.Count > 0)
            {
                return ChooseWinningCard(winningCards, plan, random);
            }

            return ChooseDiscardCard(legalCards, leadSuit, currentWinningCard, false, random);
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
            var bid = 0;
            foreach (var card in hand)
            {
                switch (card.Suit)
                {
                    case Suit.Spades when card.Rank >= 11:
                        bid += 1;
                        break;
                    case Suit.Spades:
                        bid += 0;
                        break;
                    default:
                        if (card.Rank >= 14)
                        {
                            bid += 1;
                        }
                        else if (card.Rank >= 12)
                        {
                            bid += 0;
                        }

                        break;
                }
            }

            var spadeCount = hand.Count(card => card.Suit == Suit.Spades);
            if (spadeCount >= 5)
            {
                bid += 1;
            }

            return System.Math.Clamp(bid, 1, 7);
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
            return new AiPlayPlan(teamBid, teamBooks, booksNeeded, booksRemaining, pressure);
        }

        private static Card ChooseLeadCard(List<Card> legalCards, AiPlayPlan plan, System.Random random)
        {
            var nonSpades = legalCards.Where(card => card.Suit != Suit.Spades).ToList();
            var leadPool = nonSpades.Count > 0 ? nonSpades : legalCards;
            if (plan.NeedsBookNow)
            {
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

            var safeLeads = leadPool
                .OrderBy(card => LeadRisk(card, legalCards))
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();
            return ChooseFromWindow(safeLeads, plan.NeedsBookNow ? 3 : 2, random);
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
            System.Random random)
        {
            var losingCards = legalCards
                .Where(card => !Beats(card, currentWinningCard, leadSuit))
                .OrderBy(card => DiscardCost(card, partnerWinning))
                .ThenBy(card => card.Rank)
                .ThenBy(card => card.Suit)
                .ToList();

            if (losingCards.Count > 0)
            {
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
            public AiPlayPlan(int teamBid, int teamBooks, int booksNeeded, int booksRemaining, bool underPressure)
            {
                TeamBid = teamBid;
                TeamBooks = teamBooks;
                BooksNeeded = booksNeeded;
                BooksRemaining = booksRemaining;
                UnderPressure = underPressure;
            }

            public int TeamBid { get; }
            public int TeamBooks { get; }
            public int BooksNeeded { get; }
            public int BooksRemaining { get; }
            public bool NeedsBookNow => BooksNeeded > 0;
            public bool UnderPressure { get; }
        }
    }
}
