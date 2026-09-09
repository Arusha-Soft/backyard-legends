using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public static class SpadesNetworkStateApplier
    {
        public static void ApplyPublicState(MatchState target, SpadesNetworkPublicState source, SeatId localSeat, IReadOnlyList<Card> localHand, SpadesSeatMapper mapper)
        {
            if (target == null || source == null)
            {
                return;
            }

            var logical = source.ToMatchState(localSeat, localHand);
            if (mapper != null && mapper.LocalLogicalSeat != SeatId.Bottom)
            {
                logical = RotateState(logical, mapper);
            }

            CopyMatchState(logical, target);
        }

        public static MatchState RotateState(MatchState source, SpadesSeatMapper mapper)
        {
            var rotated = new MatchState
            {
                Phase = source.Phase,
                TargetScore = source.TargetScore,
                WinningTeam = source.WinningTeam,
                RuleSet = source.RuleSet?.CloneForTarget(source.TargetScore) ?? new RuleSetDefinition { TargetScore = source.TargetScore }
            };

            foreach (var score in source.Scores)
            {
                rotated.Scores[score.Key] = CloneScore(score.Value);
            }

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var visual = mapper.ToVisual(seat);
                if (source.SeatNames.TryGetValue(seat, out var name))
                {
                    rotated.SeatNames[visual] = name;
                }
            }

            if (source.RoundState == null)
            {
                return rotated;
            }

            rotated.RoundState = new RoundState
            {
                RoundNumber = source.RoundState.RoundNumber,
                Dealer = mapper.ToVisual(source.RoundState.Dealer),
                LastStatusMessage = source.RoundState.LastStatusMessage
            };
            rotated.RoundState.BidState.CurrentBidder = mapper.ToVisual(source.RoundState.BidState.CurrentBidder);
            rotated.RoundState.TrickState.Leader = mapper.ToVisual(source.RoundState.TrickState.Leader);
            rotated.RoundState.TrickState.CurrentTurn = mapper.ToVisual(source.RoundState.TrickState.CurrentTurn);
            rotated.RoundState.TrickState.LeadSuit = source.RoundState.TrickState.LeadSuit;
            rotated.RoundState.TrickState.SpadesBroken = source.RoundState.TrickState.SpadesBroken;

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                var visual = mapper.ToVisual(seat);
                if (source.RoundState.BidState.BidsBySeat.TryGetValue(seat, out var bid))
                {
                    rotated.RoundState.BidState.BidsBySeat[visual] = bid;
                }

                if (source.RoundState.TricksWonBySeat.TryGetValue(seat, out var tricks))
                {
                    rotated.RoundState.TricksWonBySeat[visual] = tricks;
                }

                if (source.RoundState.HandsBySeat.TryGetValue(seat, out var hand))
                {
                    rotated.RoundState.HandsBySeat[visual] = hand.ToList();
                }
            }

            foreach (var play in source.RoundState.TrickState.Plays)
            {
                rotated.RoundState.TrickState.Plays.Add(new TrickPlay
                {
                    Seat = mapper.ToVisual(play.Seat),
                    Card = play.Card
                });
            }

            foreach (var renege in source.RoundState.RenegeSeats)
            {
                rotated.RoundState.RenegeSeats.Add(mapper.ToVisual(renege));
            }

            foreach (var trick in source.RoundState.CompletedTricks)
            {
                rotated.RoundState.CompletedTricks.Add(trick.Select(play => new TrickPlay
                {
                    Seat = mapper.ToVisual(play.Seat),
                    Card = play.Card
                }).ToList());
            }

            return rotated;
        }

        public static SpadesMatchEvent ToMatchEvent(SpadesNetworkEventPayload payload, MatchState snapshot, SpadesSeatMapper mapper)
        {
            var seat = mapper != null ? mapper.ToVisual((SeatId)payload.Seat) : (SeatId)payload.Seat;
            switch ((SpadesNetworkEventKind)payload.Kind)
            {
                case SpadesNetworkEventKind.MatchStarted:
                    return new MatchStartedEvent(snapshot);
                case SpadesNetworkEventKind.RoundStarted:
                    return new RoundStartedEvent(snapshot);
                case SpadesNetworkEventKind.BidSubmitted:
                    return new BidSubmittedEvent(snapshot, seat, payload.Bid);
                case SpadesNetworkEventKind.CardPlayed:
                    return new CardPlayedEvent(snapshot, seat, payload.Card.ToCard());
                case SpadesNetworkEventKind.TrickResolved:
                    var completed = payload.CompletedTrick != null
                        ? payload.CompletedTrick.Select(play => new TrickPlay
                        {
                            Seat = mapper != null ? mapper.ToVisual((SeatId)play.Seat) : (SeatId)play.Seat,
                            Card = play.Card.ToCard()
                        }).ToList()
                        : new List<TrickPlay>();
                    return new TrickResolvedEvent(snapshot, seat, completed);
                case SpadesNetworkEventKind.RoundScored:
                    return new RoundScoredEvent(snapshot, payload.Message ?? string.Empty);
                case SpadesNetworkEventKind.RemainingBooksClaimed:
                    return new RemainingBooksClaimedEvent(snapshot, (TeamId)payload.Team, payload.ClaimedBooks);
                case SpadesNetworkEventKind.MatchEnded:
                    return new MatchEndedEvent(snapshot, (TeamId)payload.WinningTeam);
                case SpadesNetworkEventKind.MatchForfeited:
                    return new MatchForfeitedEvent(snapshot, (TeamId)payload.Team, (TeamId)payload.WinningTeam);
                case SpadesNetworkEventKind.SetBookReached:
                    return new SetBookReachedEvent(snapshot, (TeamId)payload.Team);
                case SpadesNetworkEventKind.PlayerAway:
                case SpadesNetworkEventKind.PlayerReturned:
                case SpadesNetworkEventKind.CatchUpState:
                case SpadesNetworkEventKind.TableReady:
                case SpadesNetworkEventKind.SeatAssigned:
                case SpadesNetworkEventKind.ActionRejected:
                    return null;
                default:
                    return null;
            }
        }

        private static void CopyMatchState(MatchState source, MatchState target)
        {
            target.Phase = source.Phase;
            target.TargetScore = source.TargetScore;
            target.WinningTeam = source.WinningTeam;
            target.RuleSet = source.RuleSet?.CloneForTarget(source.TargetScore) ?? target.RuleSet;
            target.SeatNames.Clear();
            foreach (var pair in source.SeatNames)
            {
                target.SeatNames[pair.Key] = pair.Value;
            }

            target.Scores.Clear();
            foreach (var pair in source.Scores)
            {
                target.Scores[pair.Key] = CloneScore(pair.Value);
            }

            if (source.RoundState == null)
            {
                target.RoundState = null;
                return;
            }

            target.RoundState = new RoundState
            {
                RoundNumber = source.RoundState.RoundNumber,
                Dealer = source.RoundState.Dealer,
                LastStatusMessage = source.RoundState.LastStatusMessage
            };
            target.RoundState.BidState.CurrentBidder = source.RoundState.BidState.CurrentBidder;
            foreach (var bid in source.RoundState.BidState.BidsBySeat)
            {
                target.RoundState.BidState.BidsBySeat[bid.Key] = bid.Value;
            }

            target.RoundState.TrickState.Leader = source.RoundState.TrickState.Leader;
            target.RoundState.TrickState.CurrentTurn = source.RoundState.TrickState.CurrentTurn;
            target.RoundState.TrickState.LeadSuit = source.RoundState.TrickState.LeadSuit;
            target.RoundState.TrickState.SpadesBroken = source.RoundState.TrickState.SpadesBroken;
            target.RoundState.TrickState.Plays.Clear();
            target.RoundState.TrickState.Plays.AddRange(source.RoundState.TrickState.Plays.Select(play => new TrickPlay
            {
                Seat = play.Seat,
                Card = play.Card
            }));

            foreach (var seat in SpadesSeatUtility.TurnOrder)
            {
                target.RoundState.TricksWonBySeat[seat] = source.RoundState.TricksWonBySeat.TryGetValue(seat, out var tricks) ? tricks : 0;
                target.RoundState.HandsBySeat[seat] = source.RoundState.HandsBySeat.TryGetValue(seat, out var hand)
                    ? hand.ToList()
                    : new List<Card>();
            }

            target.RoundState.RenegeSeats.Clear();
            target.RoundState.RenegeSeats.AddRange(source.RoundState.RenegeSeats);
            target.RoundState.CompletedTricks.Clear();
            foreach (var trick in source.RoundState.CompletedTricks)
            {
                target.RoundState.CompletedTricks.Add(trick.Select(play => new TrickPlay
                {
                    Seat = play.Seat,
                    Card = play.Card
                }).ToList());
            }
        }

        private static ScoreSnapshot CloneScore(ScoreSnapshot source)
        {
            return new ScoreSnapshot
            {
                Team = source.Team,
                Score = source.Score,
                Bags = source.Bags,
                ContractBid = source.ContractBid,
                TricksWon = source.TricksWon,
                RoundDelta = source.RoundDelta,
                NilDelta = source.NilDelta,
                RenegeDelta = source.RenegeDelta,
                BagsEarned = source.BagsEarned,
                BagPenaltyDelta = source.BagPenaltyDelta
            };
        }
    }
}
