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
            if (trick.Plays.Count == 0)
            {
                return legalCards
                    .OrderBy(card => card.Suit == Suit.Spades)
                    .ThenBy(card => card.Rank)
                    .First();
            }

            var currentWinningSeat = new SpadesRuleEngine().ResolveTrickWinner(trick);
            var currentWinningCard = trick.Plays.First(play => play.Seat == currentWinningSeat).Card;
            var leadSuit = trick.LeadSuit ?? trick.Plays[0].Card.Suit;

            var winningCards = legalCards
                .Where(card => Beats(card, currentWinningCard, leadSuit))
                .OrderBy(card => card.Suit == Suit.Spades ? 0 : 1)
                .ThenBy(card => card.Rank)
                .ToList();

            if (winningCards.Count > 0)
            {
                return winningCards.First();
            }

            return legalCards.OrderBy(card => card.Rank).ThenBy(card => card.Suit).First();
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
    }
}
