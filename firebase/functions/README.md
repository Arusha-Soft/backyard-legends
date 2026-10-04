# Backyard Legends Cloud Functions

## Deploy status

- **Firestore rules:** deploy with `firebase deploy --only firestore:rules --project backyard-legends` (no Blaze required).
- **Cloud Functions** (`queueForMatch`, `cancelQueue`, `sweepStaleTables`): require the Firebase **Blaze (pay-as-you-go)** plan.

Upgrade: https://console.firebase.google.com/project/backyard-legends/usage/details

Then:

```bash
cd firebase/functions
npm install
cd ..
firebase deploy --only functions,firestore:rules --project backyard-legends
```

## Without Functions

The Unity client (`MatchmakingService`) already runs a Firestore transactional Quick Match matcher for Editor / device testing. Cloud Functions are the production path once Blaze is enabled.
