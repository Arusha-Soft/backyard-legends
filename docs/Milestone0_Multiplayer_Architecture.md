# Backyard Legends — Milestone 0
## Multiplayer Technical Architecture & Implementation Baseline

**Status:** Client-approved (rules corrected per review)  
**Stack:** Unity Netcode for GameObjects (NGO) + Firebase  
**Client:** Unity 6000.0.60f1 · Android + iOS, shared matchmaking pool  
**Table:** 4 players, 2v2, real-time (not delayed turn-based)

This document is the Milestone 0 deliverable: the approved multiplayer technical architecture and implementation baseline. Live online implementation can proceed with the rules in §8.

---

## 1. Recommendation (one page)

| Layer | Choice | Why |
| --- | --- | --- |
| Live table | **NGO 2.13** (Unity Transport) | Real-time 4-player sync, already in the project, matches “not turn-based delay.” |
| NAT / mobile connect | **Unity Relay** | Firebase cannot carry NGO UDP. Relay is the only extra Unity service we need. |
| Accounts, lobby, ranking, history | **Firebase** Auth, Firestore, Cloud Functions, FCM | Persistence, matchmaking, profiles, reconnect tokens, ratings. |
| Match simulation | **Host-authoritative `SpadesMatchController`** | Phase 1 already owns bids, tricks, scoring, claim, forfeit, renege. Host runs it; clients send actions. |
| Host model | **Listen server (one seated player hosts)** | Correct cost for a card table. Canonical public snapshot is mirrored to Firestore so a client can be promoted if the host drops. |

**What we are not building in this phase:** dedicated Unity game servers, delayed-move / play-by-mail, separate iOS vs Android pools, or ranked anti-cheat that hides cards from the host.

---

## 2. Phase 1 architecture review

Phase 1 is a complete **local** Spades product. Online work reuses it; it does not replace it.

### 2.1 Split that already exists

```
LobbyScene  →  BackyardLegendsSession (mode, target score)
GameplayScene → BackyardLegendsBootstrap (UI / animation)
                 ↓
            SpadesMatchController  (match authority)
                 ↓
            SpadesRuleEngine       (legal bids/cards, trick winner, scoring)
```

- **Core** (`BackyardLegends.Core`) is Unity-scene independent: `MatchState`, phases, events, Classic vs Street rules. This is the online authority.
- **Runtime** is presentation: deal animation, bid sheet, table, scoreboard, claim/forfeit prompts. Online clients keep this layer and drive it from network events instead of a local AI loop.
- **Session** currently stores only theme, mode, and target score, then loads gameplay. Online extends this with Firebase user + table session id.

### 2.2 Match phases we will network as-is

`Lobby → Bidding → TrickPlay → RoundSummary → MatchEnded`

Existing events already describe every table beat: match/round start, bid, card play, trick resolve, remaining books claimed, round scored, set-book, forfeit, match end. NGO will transport these events. The UI already consumes them.

### 2.3 Rules already implemented (keep)

| Rule | Phase 1 behavior |
| --- | --- |
| Table | 4 seats, Home (Bottom+Top) vs Away (Left+Right) |
| Bid floor | Team bid must reach 4 |
| Nil | Only if losing by ≥ 150 (`NilUnlockScoreGap`) |
| Targets | 100 / 200 / 500 |
| Classic | Spades must be broken; follow suit; **no renege** |
| Street | Spades anytime; Phase 1 prototype used a −200 score line (to be replaced for online) |
| Claim rest | Phase 1 prototype: unilateral claim during `TrickPlay` (to be replaced for online) |
| Forfeit | During bidding or live play; opponents win; scores preserved |
| Turn timer | Reserved at 30s, **currently off** |

### 2.4 Gaps Phase 1 does not cover (Milestone 0 must define)

- No accounts, no 4 human seats, no network transport.
- Human is always `SeatId.Bottom`. Online must map Firebase uid → seat.
- Claim rest UI is Home-only and unilateral. Online uses the client-confirmed challenge flow (§8.1).
- Street renege in Phase 1 is a −200 score delta. Online replaces that with **Call Renege → 3 books transferred** (§8.2).
- Host disconnect, reconnect, and mid-hand pause are undefined in Phase 1; online uses **90s** grace (§8.3).

---

## 3. Networking approach — NGO

### 3.1 Why NGO

The product requirement is **real-time** 2v2, same animation timing as Phase 1 (card flight, bid callouts, book collect). NGO RPCs + a host-owned controller give that. Firestore listeners are too slow and not a substitute for the live table.

NGO is used as a **message bus**, not as GameObject transform sync. Cards are data, not physics objects.

### 3.2 Authority

| Runs on host only | Runs on every client |
| --- | --- |
| `SpadesMatchController` + `SpadesRuleEngine` | Presentation (`BackyardLegendsBootstrap`) |
| Shuffle / deal | Local hand render from host payload |
| Legal bid / legal card | Input → ServerRpc |
| Claim propose / accept / reject, Call Renege, forfeit, round score, match end | Event playback + scoreboard |
| AI sit-in after disconnect grace | “Player away” overlay |

Clients never mutate `MatchState`. Illegal actions are rejected by the host with the same error strings Phase 1 already returns.

### 3.3 Traffic (small, deterministic)

Client → Host ServerRpcs:

- `SubmitBid(bid)`
- `PlayCard(suit, rank)`
- `ProposeClaimRemainingBooks()`
- `RespondToClaim(accept)`
- `CallRenege(accusedSeat)`
- `ForfeitMatch()`
- `ReadyForNextHand()`
- `ReconnectCatchUpRequest()`

Host → Clients ClientRpcs / named messages:

- `SeatAssigned(seat, publicPlayers)`
- `PrivateHand(cards)` — **only to that player**
- `PublicMatchEvent(eventType, payload)` — bids, plays, tricks, scores, claim, forfeit
- `ClaimPending(claimingTeam, revealedCards)` — both opponents must respond
- `ClaimResolved(accepted, claimingTeam)`
- `RenegeCalled(accuserSeat, accusedSeat, booksTransferred)`
- `TablePaused / TableResumed`
- `CatchUpState(publicState + privateHand)`

Opponents never receive other players’ remaining cards. They receive card-back counts and public trick cards only.

### 3.4 Transport note (Firebase vs Relay)

**Firebase does not replace Unity Relay.** NGO on mobile needs a relay to connect players behind NAT. We will use Unity Relay for the live socket only. All product backend (login, matchmaking, ratings, history, reconnect metadata) stays on Firebase.

---

## 4. Backend / server services — Firebase

| Service | Role |
| --- | --- |
| **Firebase Auth** | Anonymous (first launch) + Google (Android) + Sign in with Apple (iOS). Stable `uid` is the player id. |
| **Firestore** | Profiles, friends/invite codes, tables/sessions, match results, rating, reconnect blobs. |
| **Cloud Functions** | Matchmaking, session create, rating update after verified result, abuse hooks. |
| **Cloud Messaging** | “Your table is starting” / “Your turn” / “Reconnect to live hand.” |
| **App Check** | Basic client attestation on Functions. |

### 4.1 What Firebase stores vs what NGO stores

| Firebase (durable) | NGO (ephemeral, in-memory on host) |
| --- | --- |
| User profile, rating, cosmetics | Live `MatchState` |
| Table id, seat map, rule set, target score | Hands, current trick, turn |
| Relay join data, host uid | Frame-to-frame RPCs |
| Public snapshot for reconnect / host promotion | Private hands |
| Final match result (source of rating) | Transient pause flags |

Firestore is **not** the live tick. The host writes a public snapshot on every committed action (bid, play, claim, score) so reconnect and host promotion can restore the table without trusting a single client’s memory after a crash.

### 4.2 Matchmaking (Functions)

1. Player calls `queueForMatch({ mode, targetScore, region })`.
2. Function buckets by mode + target (Classic/Street × 100/200/500) and platform-agnostic pool (iOS + Android together).
3. When 4 are ready, Function creates `tables/{tableId}`, assigns seats (partners opposite), designates host, waits for host to publish Relay join data.
4. Other three clients read join data and NGO `StartClient()`.
5. Host starts the match only when 4 NGO peers are connected and ready.

Private table: host creates `tables/{id}` with an invite code; friends join by code; start when 4 ready (or later: fill with AI — out of scope unless requested).

---

## 5. Multiplayer match-state architecture

### 5.1 Canonical state

The host’s `MatchState` remains the single simulation:

- Phase, rule set, target, winning team
- Per-seat names / player ids
- Scores + bags
- Round: dealer, bids, hands, current trick, tricks won, completed tricks, renege seats

That object already snapshots (`CreateSnapshot()`). Online adds:

- `playerIdBySeat`
- `connectionStateBySeat` (`Connected`, `Grace`, `AiSitIn`)
- `tablePauseReason`
- `actionSeq` (monotonic, for catch-up and duplicate RPC ignore)

### 5.2 Privacy filter

Before any state leaves the host:

| Field | Local player | Opponent | Firestore public snapshot |
| --- | --- | --- | --- |
| Own hand | Full | Hidden (count only), except during pending Claim Remaining Books when claiming team is revealed | Hidden |
| Opponent hands | Count only | Own full | Hidden |
| Bids, books, scores | Public | Public | Public |
| Current trick cards | Public | Public | Public |
| Renege seats | Public in Street | Public in Street | Public |
| RNG seed | Host only | Never | Never |

### 5.3 Determinism

Shuffle stays on the host (`System.Random` with a host seed). Clients do not simulate the deck. After each committed action, `actionSeq` increments. Catch-up is “send filtered snapshot at seq N,” not replay of every animation, then the client jumps the UI to the current phase.

### 5.4 UI contract

`BackyardLegendsBootstrap` already renders from `SpadesMatchEvent`. The network layer will raise the same events on clients after each host broadcast. Opening deal / card flight stay local and cosmetic; they must not gate host authority. If a late joiner arrives mid-trick, skip deal cinematic and show the current table.

---

## 6. Session architecture

```
Firebase Auth
    → Profile (uid, displayName, rating, platform)
    → Queue or Invite
    → Table session (Firestore)
         → Host allocates Unity Relay
         → 4× NGO connect
         → Host runs match
         → Per-action public snapshot
         → MatchEnded → Functions write result + rating
         → NGO shutdown, table archived
```

### 6.1 Table document (Firestore)

```
tables/{tableId}
  status: matching | waiting_relay | in_play | paused | completed | abandoned
  mode: Classic | Street
  targetScore: 100 | 200 | 500
  ranked: false          // ranked flag reserved; rating still recorded if enabled later
  hostUid
  relay: { joinCode, region }     // short-lived
  seats: {
    Bottom: { uid, displayName, conn },
    Left:   { ... },
    Top:    { ... },
    Right:  { ... }
  }
  publicSnapshot: { phase, scores, bids, trick, books, renegeSeats, actionSeq, ... }
  createdAt, startedAt, endedAt
  result: { winner, scores, reason }   // set once
```

### 6.2 Client session object (Unity)

Replace “mode + target → load scene” with:

1. Authenticate.
2. Resolve table (matchmade or invite).
3. If host: `StartHost` + publish Relay; if client: `StartClient`.
4. Load `GameplayScene` only after seat assignment.
5. Bind local seat (camera/hand at Bottom **visually**, logical seat from network).
6. On match end: write-ack from Firestore, return to lobby.

Visual Bottom-seat layout is preserved: each client remaps their logical seat to the Phase 1 Bottom/Left/Top/Right presentation.

### 6.3 Ready / next hand

Round summary is synchronized. Host does not deal the next hand until all **connected** humans have pressed Next Hand (AI sit-ins are auto-ready). Timeout: 30s then host auto-ready that seat.

---

## 7. Reconnect architecture

### 7.1 Client disconnect (host still live)

1. Host marks seat `Grace`, pauses the table, broadcasts `TablePaused`.
2. Firestore `seats.{seat}.conn = grace`.
3. **Grace period: 90 seconds.** Timer visible to all.
4. Disconnected client: Auth uid + `tableId` → read Relay join (or refresh via Function if allocation rotated) → NGO reconnect → `ReconnectCatchUpRequest`.
5. Host validates uid ↔ seat, sends filtered snapshot + private hand, marks `Connected`, `TableResumed`.
6. If grace expires: seat becomes `AiSitIn`. Match continues. Original uid may reclaim the seat until `MatchEnded` (casual baseline).

### 7.2 Host disconnect

1. Clients pause, Firestore lease on `hostUid` expires (heartbeat every 10s; dead after 20s).
2. **Grace: 90 seconds** for original host to return.
3. If host returns: same catch-up path.
4. If not: Cloud Function (or remaining clients by seat order) **promotes** a new host.
5. New host loads last `publicSnapshot` from Firestore, remaining players reconnect to a **new** Relay allocation, new host asks each player for a **hand hash**; if a private hand cannot be proven, that hand is restored only if the player still holds it locally and the hash matches the last host write of `handCount` + optional per-player encrypted hand blob.

**Private-hand restore:** on every deal and after every play, the host writes an **encrypted per-uid hand blob** (keyed so only that uid’s client can decrypt, plus a server-side integrity hash). Promotion can restore hands without revealing them to the new host’s screen; the new host process still decrypts them in memory to continue simulation (listen-server limitation — see §7.4).

### 7.3 App background / network blip

Mobile will background. Treat as disconnect:

- NGO: disconnect timeout ~10s.
- If the player returns inside grace, seamless catch-up.
- Turn timer (when enabled) does **not** run while the table is paused for disconnect.

### 7.4 Honest limitation (ranked later)

Listen-server means the host process can see all hands. Fine for friends / unranked. **Ranked with prize or leaderboard integrity** needs dedicated simulation (Cloud Function per action, or a dedicated server). That is **not** Milestone 1–2. Flag it as Phase 3 if ranking becomes competitive.

---

## 8. Rule confirmations (client-approved)

These replace Phase 1 prototype behavior where it differed. Locked per client review.

### 8.1 Claim Remaining Books — APPROVED

**Online rule:**

- Allowed only in `TrickPlay`.
- Any player may propose a claim for **their own team**.
- On propose: the **claiming team’s cards are revealed** to the table.
- **Both opponents must accept.**
- If **either** opponent rejects: the claim is **rejected**, cards return to hidden hands, and the **hand continues**.
- If **both** accept: remaining books are awarded to the claiming team, hands clear, hand is scored immediately.
- Incomplete current trick is discarded (not resolved) only when the claim is accepted.
- Remaining books = max cards still in any hand (unplayed tricks).
- Award seat: prefer a **non-Nil** partner, then seat order.
- Host validates all responses; clients show claim-pending UI until both opponents answer.

### 8.2 Call Renege — Street Mode only — APPROVED

**Classic**

- Follow suit is **enforced**. Off-suit while holding lead is rejected. No Call Renege. No book transfer.

**Street**

- If a player fails to follow suit when they could, an opponent may **Call Renege**.
- Call Renege is **Street Mode only**.
- When a call is upheld (host confirms the accused held the lead suit and played off-suit):
  - Guilty team **loses 3 books**.
  - Opposing team is **awarded those 3 books**.
  - This is a **book transfer**, not a −200 score penalty.
- The illegal card still stands in the trick (trick is not rewound); books are adjusted when the call is resolved.
- Phase 1’s −200 `RenegeDelta` score line is **not** used online; scoring UI shows the 3-book transfer instead.

### 8.3 Disconnect / reconnect — APPROVED

| Event | Behavior |
| --- | --- |
| Any human drops during bidding or play | Table **pauses**; **90s** reconnect grace |
| Returns in grace | Catch-up; resume; no penalty |
| Grace expires (unranked) | **AI sits in**; match continues; player may reclaim until match end |
| Grace expires (ranked — later) | **Team forfeit** (not shipped until ranked is on) |
| Host drops, returns in grace | Same table resumes |
| Host drops, grace expires | Host promotion from Firestore snapshot |
| Leave / Forfeit button | Immediate opponent win (existing `TryForfeitMatch`) |
| All opponents gone | Match abandoned; no rating change |

Turn timer remains **off** until reconnect is stable; then enable 30s per bid/play, paused during disconnect grace.

---

## 9. Cross-platform requirements

| Requirement | Baseline |
| --- | --- |
| Platforms in one pool | **Android + iOS** |
| Windows / Editor | Dev and Multiplayer Play Mode only, not production matchmaking |
| Orientation | Portrait (existing) |
| Min OS | Android API 23 · iOS 13 (current project) |
| Auth | Apple required on iOS; Google on Android; anonymous allowed then upgrade |
| Relay regions | Pin to closest of `us`, `europe`, `asia` from client; matchmaking prefers same region but does not split by OS |
| Time / randomness | Host clock + host RNG; no client time |
| Version gate | Firestore `minClientVersion`; mismatch → update prompt, no queue |
| Input | Existing uGUI; NGO does not replicate UI transforms |

Testing: Unity **Multiplayer Play Mode** (already installed) for 4 editor instances, plus 1 Android + 1 iOS device on the same Firebase project.

---

## 10. Implementation plan

Reuse Core. Do not rewrite scoring. Add a thin network + Firebase shell.

### Milestone 1 — NGO table in editor
- NetworkManager + Unity Transport + Relay (dev)
- Host runs `SpadesMatchController`
- 2–4 Multiplayer Play Mode clients: bid, play, score, claim, forfeit
- Seat remap so each human is visual Bottom
- Private hands
- No Firebase required for this milestone (Editor host + LAN/Relay)

### Milestone 2 — Firebase session
- Auth (anonymous + Google + Apple)
- Table document, invite code, queue stub (can be manual 4-player join)
- Bind uid → seat
- Persist public snapshot + encrypted hands
- Return to lobby on match end; write result

### Milestone 3 — Disconnect / reconnect
- Pause + **90s** grace
- Client catch-up
- AI sit-in
- Host heartbeat + promotion
- Backgrounding on device

### Milestone 4 — Matchmaking + claim / Call Renege live
- Cloud Function matchmaking (mode × target, iOS+Android)
- Claim Remaining Books: reveal claiming team + both opponents must accept
- Street Call Renege: upheld call transfers **3 books** to the opposing team
- FCM optional
- Version gate

### Milestone 5 — Production hardening
- App Check, abuse / rematch spam limits
- Rating write on verified `MatchEnded` only
- Region selection, connection quality HUD
- Ranked forfeit-on-timeout (if ranked is in scope)

---

## 11. Approval checklist

Milestone 0 architecture approved pending the three rule corrections below (now applied):

- [x] NGO listen-server + Unity Relay for the live table; Firebase for accounts, tables, matchmaking, persistence, ranking
- [x] Host-authoritative `SpadesMatchController`; clients are presentation + input
- [x] iOS and Android share one matchmaking pool
- [x] Claim Remaining Books as in §8.1 (reveal claiming team; both opponents must accept; either reject continues the hand)
- [x] Call Renege as in §8.2 (Street only; upheld call = 3 books from guilty team to opposing team; not −200)
- [x] Disconnect as in §8.3 (**90s** pause, then AI sit-in for unranked)
- [x] Ranked host-trust limitation accepted until a later dedicated-authority milestone
- [x] Implementation order: M1 NGO table → M2 Firebase session → M3 reconnect → M4 matchmaking → M5 hardening

**Status:** Client review accepted overall architecture; rule corrections from client feedback are incorporated. Ready to move forward on implementation.
