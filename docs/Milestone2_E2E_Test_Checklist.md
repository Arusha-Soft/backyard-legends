# Milestone 2 — E2E Test Checklist (Path 1)

Use this after linking Team UGS (`Backyard Legends/Verify UGS Project Link`).

## Step 0 — Relay

- [ ] `Application.cloudProjectId` is non-empty
- [ ] Host Table logs a Relay join code (not `LOCAL`)
- [ ] Second editor/device joins with that code across processes

## Step 1 — Private room

- [ ] Host creates room → Firestore `tables/{id}` status `waiting_lobby`
- [ ] 2–4 clients join by invite code
- [ ] Client Leave clears seat in Firestore
- [ ] Host Leave abandons table (`status: abandoned`)

## Step 2 — Lobby ready / start

- [ ] Gameplay shows Multiplayer Lobby HUD (seats, Home/Away, Ready)
- [ ] Deal does **not** start with AI fill after 45s
- [ ] Start / auto-deal only when 4 humans are Ready
- [ ] After start, HUD hides and cards deal

## Step 3 — Invite

- [ ] Copy Code puts invite on clipboard
- [ ] Share (Android) opens system share sheet
- [ ] Second device joins using only the shared code (no LAN IP)

## Step 4 — Reconnect

- [ ] Mid-hand force-quit client → table pauses (90s grace)
- [ ] Rejoin same account within 90s → same seat + catch-up
- [ ] After grace → AI sit-in; original uid can reclaim
- [ ] Bad Relay code / unlinked UGS shows actionable error (no silent LOCAL)

## Step 5 — Quick Match

- [ ] Four clients same mode+target press Quick Match
- [ ] Matched into one Firestore table; host publishes Relay code
- [ ] Others join NGO; lobby ready gate applies
- [ ] Cancel Queue returns to idle lobby status

Optional Functions deploy:

```bash
cd firebase/functions && npm install
firebase deploy --only functions,firestore:rules
```

## Step 6 — Cleanup

- [ ] Match end marks `completed`
- [ ] Leave/abandon shuts NGO and returns to LobbyScene
- [ ] Immediate Host / Join / Quick Match works again without Editor restart
- [ ] `sweepStaleTables` (or manual) abandons dead waiting rooms

## Deliverable

Players can reliably find, create, join, leave, and reconnect to multiplayer matches.
