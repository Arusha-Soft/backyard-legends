using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BackyardLegends.Core;

namespace BackyardLegends.Runtime.Firebase
{
    /// <summary>
    /// Firestore-friendly encode/decode for MatchState + per-uid hand blobs (HMAC integrity with session key).
    /// </summary>
    public static class MatchStateFirestoreCodec
    {
        public static Dictionary<string, object> EncodeFullAuthorityState(MatchState state, int actionSeq)
        {
            var root = new Dictionary<string, object>
            {
                ["actionSeq"] = actionSeq,
                ["phase"] = (int)(state?.Phase ?? MatchPhase.Lobby),
                ["targetScore"] = state?.TargetScore ?? 100,
                ["hasWinningTeam"] = state?.WinningTeam != null,
                ["winningTeam"] = state?.WinningTeam != null ? (int)state.WinningTeam.Value : 0,
                ["statusMessage"] = state?.RoundState?.LastStatusMessage ?? string.Empty,
                ["roundNumber"] = state?.RoundState?.RoundNumber ?? 0,
                ["dealer"] = (int)(state?.RoundState?.Dealer ?? SeatId.Bottom),
                ["currentBidder"] = (int)(state?.RoundState?.BidState?.CurrentBidder ?? SeatId.Bottom),
                ["trickLeader"] = (int)(state?.RoundState?.TrickState?.Leader ?? SeatId.Bottom),
                ["currentTurn"] = (int)(state?.RoundState?.TrickState?.CurrentTurn ?? SeatId.Bottom),
                ["hasLeadSuit"] = state?.RoundState?.TrickState?.LeadSuit != null,
                ["leadSuit"] = state?.RoundState?.TrickState?.LeadSuit != null
                    ? (int)state.RoundState.TrickState.LeadSuit.Value
                    : 0,
                ["spadesBroken"] = state?.RoundState?.TrickState?.SpadesBroken == true
            };

            var seatNames = new List<object>();
            var bids = new List<object>();
            var tricksWon = new List<object>();
            var handCounts = new List<object>();
            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                seatNames.Add(state?.SeatNames != null && state.SeatNames.TryGetValue(seat, out var name)
                    ? name
                    : seat.ToString());
                if (state?.RoundState?.BidState?.BidsBySeat != null &&
                    state.RoundState.BidState.BidsBySeat.TryGetValue(seat, out var bid) &&
                    bid.HasValue)
                {
                    bids.Add(bid.Value);
                }
                else
                {
                    bids.Add(null);
                }

                tricksWon.Add(state?.RoundState?.TricksWonBySeat != null &&
                              state.RoundState.TricksWonBySeat.TryGetValue(seat, out var tw)
                    ? tw
                    : 0);
                handCounts.Add(state?.RoundState?.HandsBySeat != null &&
                               state.RoundState.HandsBySeat.TryGetValue(seat, out var hand) &&
                               hand != null
                    ? hand.Count
                    : 0);
            }

            root["seatNames"] = seatNames;
            root["bids"] = bids;
            root["tricksWon"] = tricksWon;
            root["handCounts"] = handCounts;

            var scores = new List<object>();
            if (state?.Scores != null)
            {
                foreach (var pair in state.Scores)
                {
                    scores.Add(new Dictionary<string, object>
                    {
                        ["team"] = (int)pair.Value.Team,
                        ["score"] = pair.Value.Score,
                        ["bags"] = pair.Value.Bags,
                        ["contractBid"] = pair.Value.ContractBid,
                        ["tricksWon"] = pair.Value.TricksWon,
                        ["roundDelta"] = pair.Value.RoundDelta,
                        ["nilDelta"] = pair.Value.NilDelta,
                        ["renegeDelta"] = pair.Value.RenegeDelta,
                        ["bagsEarned"] = pair.Value.BagsEarned,
                        ["bagPenaltyDelta"] = pair.Value.BagPenaltyDelta
                    });
                }
            }

            root["scores"] = scores;

            var plays = new List<object>();
            if (state?.RoundState?.TrickState?.Plays != null)
            {
                foreach (var play in state.RoundState.TrickState.Plays)
                {
                    plays.Add(new Dictionary<string, object>
                    {
                        ["seat"] = (int)play.Seat,
                        ["suit"] = (int)play.Card.Suit,
                        ["rank"] = play.Card.Rank
                    });
                }
            }

            root["currentPlays"] = plays;

            var renege = new List<object>();
            if (state?.RoundState?.RenegeSeats != null)
            {
                foreach (var seat in state.RoundState.RenegeSeats)
                {
                    renege.Add((int)seat);
                }
            }

            root["renegeSeats"] = renege;

            if (state?.RuleSet != null)
            {
                root["ruleSet"] = EncodeRuleSet(state.RuleSet);
            }

            if (state?.RoundState?.HandsBySeat != null)
            {
                var hands = new Dictionary<string, object>();
                foreach (var pair in state.RoundState.HandsBySeat)
                {
                    hands[((int)pair.Key).ToString(CultureInfo.InvariantCulture)] = EncodeCards(pair.Value);
                }

                root["handsBySeat"] = hands;
            }

            if (state?.RoundState?.CompletedTricks != null && state.RoundState.CompletedTricks.Count > 0)
            {
                var completed = new List<object>();
                foreach (var trick in state.RoundState.CompletedTricks)
                {
                    completed.Add(trick.Select(play => new Dictionary<string, object>
                    {
                        ["seat"] = (int)play.Seat,
                        ["suit"] = (int)play.Card.Suit,
                        ["rank"] = play.Card.Rank
                    }).ToList());
                }

                root["completedTricks"] = completed;
            }

            return root;
        }

        public static Dictionary<string, object> EncodeHandBlobs(
            MatchState state,
            IReadOnlyDictionary<SeatId, string> uidBySeat,
            string sessionKey)
        {
            var blobs = new Dictionary<string, object>();
            if (state?.RoundState?.HandsBySeat == null)
            {
                return blobs;
            }

            foreach (var pair in state.RoundState.HandsBySeat)
            {
                if (!uidBySeat.TryGetValue(pair.Key, out var uid) || string.IsNullOrWhiteSpace(uid))
                {
                    continue;
                }

                var cards = EncodeCards(pair.Value);
                var payload = string.Join(",", cards.Select(c => c.ToString()));
                blobs[uid] = new Dictionary<string, object>
                {
                    ["seat"] = (int)pair.Key,
                    ["cards"] = cards,
                    ["count"] = pair.Value?.Count ?? 0,
                    ["hash"] = ComputeIntegrityHash(sessionKey, uid, payload)
                };
            }

            return blobs;
        }

        public static MatchState DecodeAuthorityState(IDictionary<string, object> data, RuleSetDefinition fallbackRules)
        {
            var state = new MatchState
            {
                Phase = (MatchPhase)GetInt(data, "phase", (int)MatchPhase.Lobby),
                TargetScore = GetInt(data, "targetScore", fallbackRules?.TargetScore ?? 100),
                WinningTeam = GetBool(data, "hasWinningTeam", false)
                    ? (TeamId?)(TeamId)GetInt(data, "winningTeam", 0)
                    : null,
                RuleSet = DecodeRuleSet(GetMap(data, "ruleSet"), fallbackRules)
            };

            var seatNames = GetStringList(data, "seatNames");
            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                state.SeatNames[seat] = seatNames != null && i < seatNames.Count && !string.IsNullOrEmpty(seatNames[i])
                    ? seatNames[i]
                    : seat.ToString();
            }

            var scores = GetList(data, "scores");
            if (scores != null)
            {
                foreach (var entry in scores)
                {
                    if (entry is not IDictionary<string, object> map)
                    {
                        continue;
                    }

                    var team = (TeamId)GetInt(map, "team", 0);
                    state.Scores[team] = new ScoreSnapshot
                    {
                        Team = team,
                        Score = GetInt(map, "score", 0),
                        Bags = GetInt(map, "bags", 0),
                        ContractBid = GetInt(map, "contractBid", 0),
                        TricksWon = GetInt(map, "tricksWon", 0),
                        RoundDelta = GetInt(map, "roundDelta", 0),
                        NilDelta = GetInt(map, "nilDelta", 0),
                        RenegeDelta = GetInt(map, "renegeDelta", 0),
                        BagsEarned = GetInt(map, "bagsEarned", 0),
                        BagPenaltyDelta = GetInt(map, "bagPenaltyDelta", 0)
                    };
                }
            }

            state.RoundState = new RoundState
            {
                RoundNumber = GetInt(data, "roundNumber", 1),
                Dealer = (SeatId)GetInt(data, "dealer", 0),
                LastStatusMessage = GetString(data, "statusMessage", string.Empty)
            };
            state.RoundState.BidState.CurrentBidder = (SeatId)GetInt(data, "currentBidder", 0);
            state.RoundState.TrickState.Leader = (SeatId)GetInt(data, "trickLeader", 0);
            state.RoundState.TrickState.CurrentTurn = (SeatId)GetInt(data, "currentTurn", 0);
            state.RoundState.TrickState.LeadSuit = GetBool(data, "hasLeadSuit", false)
                ? (Suit?)(Suit)GetInt(data, "leadSuit", 0)
                : null;
            state.RoundState.TrickState.SpadesBroken = GetBool(data, "spadesBroken", false);

            var bids = GetNullableIntList(data, "bids");
            var tricksWon = GetIntList(data, "tricksWon");
            for (var i = 0; i < 4; i++)
            {
                var seat = (SeatId)i;
                state.RoundState.BidState.BidsBySeat[seat] = bids != null && i < bids.Count ? bids[i] : null;
                state.RoundState.TricksWonBySeat[seat] = tricksWon != null && i < tricksWon.Count ? tricksWon[i] : 0;
                state.RoundState.HandsBySeat[seat] = new List<Card>();
            }

            var plays = GetList(data, "currentPlays");
            if (plays != null)
            {
                foreach (var entry in plays)
                {
                    if (entry is not IDictionary<string, object> map)
                    {
                        continue;
                    }

                    state.RoundState.TrickState.Plays.Add(new TrickPlay
                    {
                        Seat = (SeatId)GetInt(map, "seat", 0),
                        Card = new Card((Suit)GetInt(map, "suit", 0), GetInt(map, "rank", 2))
                    });
                }
            }

            var renege = GetIntList(data, "renegeSeats");
            if (renege != null)
            {
                foreach (var seatIndex in renege)
                {
                    state.RoundState.RenegeSeats.Add((SeatId)seatIndex);
                }
            }

            var hands = GetMap(data, "handsBySeat");
            if (hands != null)
            {
                foreach (var pair in hands)
                {
                    if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seatIndex))
                    {
                        continue;
                    }

                    state.RoundState.HandsBySeat[(SeatId)seatIndex] = DecodeCards(pair.Value);
                }
            }

            var completed = GetList(data, "completedTricks");
            if (completed != null)
            {
                foreach (var trickObj in completed)
                {
                    if (trickObj is not IList<object> trickList)
                    {
                        continue;
                    }

                    var trick = new List<TrickPlay>();
                    foreach (var playObj in trickList)
                    {
                        if (playObj is not IDictionary<string, object> map)
                        {
                            continue;
                        }

                        trick.Add(new TrickPlay
                        {
                            Seat = (SeatId)GetInt(map, "seat", 0),
                            Card = new Card((Suit)GetInt(map, "suit", 0), GetInt(map, "rank", 2))
                        });
                    }

                    state.RoundState.CompletedTricks.Add(trick);
                }
            }

            return state;
        }

        public static bool TryApplyHandBlobs(
            MatchState state,
            IDictionary<string, object> handBlobs,
            string sessionKey)
        {
            if (state?.RoundState == null || handBlobs == null)
            {
                return false;
            }

            var applied = false;
            foreach (var pair in handBlobs)
            {
                if (pair.Value is not IDictionary<string, object> blob)
                {
                    continue;
                }

                var uid = pair.Key;
                var cards = DecodeCards(blob.TryGetValue("cards", out var raw) ? raw : null);
                var payload = string.Join(",", EncodeCards(cards).Select(c => c.ToString()));
                var expected = GetString(blob, "hash", string.Empty);
                var actual = ComputeIntegrityHash(sessionKey, uid, payload);
                if (!string.IsNullOrEmpty(expected) &&
                    !string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    continue;
                }

                var seat = (SeatId)GetInt(blob, "seat", 0);
                state.RoundState.HandsBySeat[seat] = cards;
                applied = true;
            }

            return applied;
        }

        private static List<object> EncodeCards(IReadOnlyList<Card> cards)
        {
            var list = new List<object>();
            if (cards == null)
            {
                return list;
            }

            foreach (var card in cards)
            {
                list.Add(new Dictionary<string, object>
                {
                    ["suit"] = (int)card.Suit,
                    ["rank"] = card.Rank
                });
            }

            return list;
        }

        private static List<Card> DecodeCards(object raw)
        {
            var cards = new List<Card>();
            if (raw is not IList<object> list)
            {
                return cards;
            }

            foreach (var entry in list)
            {
                if (entry is IDictionary<string, object> map)
                {
                    cards.Add(new Card((Suit)GetInt(map, "suit", 0), GetInt(map, "rank", 2)));
                }
            }

            return cards;
        }

        private static Dictionary<string, object> EncodeRuleSet(RuleSetDefinition rules)
        {
            return new Dictionary<string, object>
            {
                ["displayName"] = rules.DisplayName ?? "Classic",
                ["spadesMustBeBroken"] = rules.SpadesMustBeBroken,
                ["allowSpadesAnytime"] = rules.AllowSpadesAnytime,
                ["followSuitRequired"] = rules.FollowSuitRequired,
                ["renegePenaltyEnabled"] = rules.RenegePenaltyEnabled,
                ["renegePenaltyPoints"] = rules.RenegePenaltyPoints,
                ["nilScore"] = rules.NilScore,
                ["nilUnlockScoreGap"] = rules.NilUnlockScoreGap,
                ["minimumTeamBid"] = rules.MinimumTeamBid,
                ["maxBid"] = rules.MaxBid,
                ["bagPenaltyThreshold"] = rules.BagPenaltyThreshold,
                ["bagPenaltyPoints"] = rules.BagPenaltyPoints,
                ["targetScore"] = rules.TargetScore
            };
        }

        private static RuleSetDefinition DecodeRuleSet(IDictionary<string, object> map, RuleSetDefinition fallback)
        {
            var rules = fallback?.CloneForTarget(fallback.TargetScore) ?? new RuleSetDefinition();
            if (map == null)
            {
                return rules;
            }

            rules.DisplayName = GetString(map, "displayName", rules.DisplayName);
            rules.SpadesMustBeBroken = GetBool(map, "spadesMustBeBroken", rules.SpadesMustBeBroken);
            rules.AllowSpadesAnytime = GetBool(map, "allowSpadesAnytime", rules.AllowSpadesAnytime);
            rules.FollowSuitRequired = GetBool(map, "followSuitRequired", rules.FollowSuitRequired);
            rules.RenegePenaltyEnabled = GetBool(map, "renegePenaltyEnabled", rules.RenegePenaltyEnabled);
            rules.RenegePenaltyPoints = GetInt(map, "renegePenaltyPoints", rules.RenegePenaltyPoints);
            rules.NilScore = GetInt(map, "nilScore", rules.NilScore);
            rules.NilUnlockScoreGap = GetInt(map, "nilUnlockScoreGap", rules.NilUnlockScoreGap);
            rules.MinimumTeamBid = GetInt(map, "minimumTeamBid", rules.MinimumTeamBid);
            rules.MaxBid = GetInt(map, "maxBid", rules.MaxBid);
            rules.BagPenaltyThreshold = GetInt(map, "bagPenaltyThreshold", rules.BagPenaltyThreshold);
            rules.BagPenaltyPoints = GetInt(map, "bagPenaltyPoints", rules.BagPenaltyPoints);
            rules.TargetScore = GetInt(map, "targetScore", rules.TargetScore);
            return rules;
        }

        private static string ComputeIntegrityHash(string sessionKey, string uid, string payload)
        {
            var key = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(sessionKey) ? "backyard" : sessionKey);
            var data = Encoding.UTF8.GetBytes((uid ?? string.Empty) + "|" + (payload ?? string.Empty));
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(data));
        }

        public static string GetString(IDictionary<string, object> data, string key, string fallback)
        {
            if (data != null && data.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }

            return fallback;
        }

        public static int GetInt(IDictionary<string, object> data, string key, int fallback)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool GetBool(IDictionary<string, object> data, string key, bool fallback)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static double GetDouble(IDictionary<string, object> data, string key, double fallback)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static IDictionary<string, object> GetMap(IDictionary<string, object> data, string key)
        {
            if (data != null && data.TryGetValue(key, out var value))
            {
                if (value is IDictionary<string, object> map)
                {
                    return map;
                }

                // Firestore may return Dictionary<string, object> subclasses
                if (value is System.Collections.IDictionary dict)
                {
                    var converted = new Dictionary<string, object>();
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        converted[entry.Key.ToString()] = entry.Value;
                    }

                    return converted;
                }
            }

            return null;
        }

        public static List<object> GetList(IDictionary<string, object> data, string key)
        {
            if (data != null && data.TryGetValue(key, out var value) && value is System.Collections.IEnumerable enumerable &&
                value is not string)
            {
                return enumerable.Cast<object>().ToList();
            }

            return null;
        }

        public static List<string> GetStringList(IDictionary<string, object> data, string key)
        {
            var list = GetList(data, key);
            return list?.Select(v => v?.ToString() ?? string.Empty).ToList();
        }

        public static List<int> GetIntList(IDictionary<string, object> data, string key)
        {
            var list = GetList(data, key);
            if (list == null)
            {
                return null;
            }

            var result = new List<int>(list.Count);
            foreach (var item in list)
            {
                try
                {
                    result.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
                }
                catch
                {
                    result.Add(0);
                }
            }

            return result;
        }

        public static List<int?> GetNullableIntList(IDictionary<string, object> data, string key)
        {
            var list = GetList(data, key);
            if (list == null)
            {
                return null;
            }

            var result = new List<int?>(list.Count);
            foreach (var item in list)
            {
                if (item == null)
                {
                    result.Add(null);
                    continue;
                }

                try
                {
                    result.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
                }
                catch
                {
                    result.Add(null);
                }
            }

            return result;
        }
    }
}
