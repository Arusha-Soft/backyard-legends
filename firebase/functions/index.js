/**
 * Backyard Legends — Milestone 2 Cloud Functions
 *
 * Deploy:
 *   cd firebase/functions && npm install
 *   firebase deploy --only functions,firestore:rules
 *
 * Client also runs a Firestore transactional matcher for Editor testing
 * when Functions are not deployed yet.
 */
const functions = require("firebase-functions");
const admin = require("firebase-admin");

admin.initializeApp();
const db = admin.firestore();

const QUEUE = "matchQueue";
const TABLES = "tables";

exports.queueForMatch = functions.https.onCall(async (data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError("unauthenticated", "Sign in required.");
  }

  const uid = context.auth.uid;
  const mode = (data && data.mode) || "Classic";
  const targetScore = (data && data.targetScore) || 100;
  const region = (data && data.region) || "auto";
  const displayName = (data && data.displayName) || "Player";
  const bucketId = `${mode}_${targetScore}_${region}`.replace(/\s/g, "");
  const now = Math.floor(Date.now() / 1000);

  await db.collection(QUEUE).doc(uid).set(
    {
      uid,
      displayName,
      mode,
      targetScore,
      region,
      bucketId,
      status: "queued",
      tableId: "",
      joinCode: "",
      isHost: false,
      queuedAt: now,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    },
    { merge: true }
  );

  const tableId = await tryFormMatch(bucketId, mode, targetScore);
  const ticket = await db.collection(QUEUE).doc(uid).get();
  return {
    status: ticket.get("status"),
    tableId: ticket.get("tableId") || tableId || "",
    isHost: !!ticket.get("isHost"),
  };
});

exports.cancelQueue = functions.https.onCall(async (_data, context) => {
  if (!context.auth) {
    throw new functions.https.HttpsError("unauthenticated", "Sign in required.");
  }

  await db.collection(QUEUE).doc(context.auth.uid).set(
    {
      status: "cancelled",
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    },
    { merge: true }
  );
  return { ok: true };
});

/** Abandon waiting lobbies whose host lease is stale (> 10 minutes). */
exports.sweepStaleTables = functions.pubsub
  .schedule("every 10 minutes")
  .onRun(async () => {
    const now = Math.floor(Date.now() / 1000);
    const cutoff = now - 600;
    const snap = await db
      .collection(TABLES)
      .where("status", "in", ["waiting_lobby", "waiting_relay", "paused"])
      .limit(40)
      .get();

    const batch = db.batch();
    let n = 0;
    snap.forEach((doc) => {
      const lease = doc.get("hostLeaseAt") || 0;
      if (lease > 0 && lease < cutoff) {
        batch.update(doc.ref, {
          status: "abandoned",
          abandonReason: "stale_host_lease",
          endedAt: admin.firestore.FieldValue.serverTimestamp(),
          updatedAt: admin.firestore.FieldValue.serverTimestamp(),
        });
        n += 1;
      }
    });
    if (n > 0) {
      await batch.commit();
    }
    return null;
  });

async function tryFormMatch(bucketId, mode, targetScore) {
  return db.runTransaction(async (tx) => {
    const query = await db
      .collection(QUEUE)
      .where("bucketId", "==", bucketId)
      .where("status", "==", "queued")
      .limit(12)
      .get();

    if (query.size < 4) {
      return "";
    }

    const ordered = query.docs
      .slice()
      .sort((a, b) => (a.get("queuedAt") || 0) - (b.get("queuedAt") || 0))
      .slice(0, 4);

    const hostUid = ordered[0].id;
    const tableRef = db.collection(TABLES).doc();
    const now = Math.floor(Date.now() / 1000);
    const seats = {
      Bottom: seat(ordered[0]),
      Top: seat(ordered[1]),
      Left: seat(ordered[2]),
      Right: seat(ordered[3]),
    };

    tx.set(tableRef, {
      status: "waiting_lobby",
      mode,
      targetScore,
      ranked: false,
      hostUid,
      joinCode: "",
      sessionKey: tableRef.id.replace(/[^a-zA-Z0-9]/g, "").slice(0, 24),
      hostLeaseAt: now,
      matchmade: true,
      seats,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    for (let i = 0; i < ordered.length; i += 1) {
      tx.update(ordered[i].ref, {
        status: "matched",
        tableId: tableRef.id,
        isHost: ordered[i].id === hostUid,
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      });
    }

    return tableRef.id;
  });
}

function seat(doc) {
  return {
    uid: doc.id,
    displayName: doc.get("displayName") || "Player",
    conn: "connected",
    ready: false,
  };
}
