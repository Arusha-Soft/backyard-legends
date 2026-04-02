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
            if (context.LegalBids.Contains(0) && estimatedTricks <= 2)
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
