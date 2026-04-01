# 🔑 GitHub Secrets Setup

These secrets are required for CI/CD Unity builds using **GitHub Actions** with [game-ci/unity-builder](https://game.ci/).

---

## 🎮 Unity (Required)

| Secret               | Description                                                                                  | How to Get                                                                                                                                                                                                                                    |
| -------------------- | -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`UNITY_LICENSE`**  | Unity license file (`.ulf`) in **base64** format. Preferred activation method.               | 1. On your dev PC, open **Unity Hub → Preferences → License → Activate new license**.2. Export license (`Unity_v20xx.x.ulf`).3. Base64 encode:`bash<br>base64 Unity_v20xx.x.ulf > unity.ulf.b64<br>`4. Copy the full string into this secret. |
| **`UNITY_EMAIL`**    | Unity account email. Alternative to `UNITY_LICENSE`.                                         | Use your Unity Hub login email.                                                                                                                                                                                                               |
| **`UNITY_PASSWORD`** | Unity account password (or activation token if 2FA enabled). Alternative to `UNITY_LICENSE`. | Use your Unity Hub login password or activation token.                                                                                                                                                                                        |

⚠️ Use **either** `UNITY_LICENSE` **or** (`UNITY_EMAIL` + `UNITY_PASSWORD`) — not both.

---

## 📢 Telegram (Optional, for build notifications)

| Secret                                     | Description                                                                 | How to Get                                                                                                                                          |
| ------------------------------------------ | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`TELEGRAM_BOT_TOKEN`**                   | Bot token used to send build notifications.                                 | Create a bot with [@BotFather](https://t.me/botfather) and copy the token.                                                                          |
| **`TELEGRAM_CHAT_ID`**                     | Numeric group ID where notifications will be sent (e.g., `-100xxxxxxxxxx`). | 1. Add your bot to the group.2. Post a message.3. Call:`<br>https://api.telegram.org/bot<YOUR_TOKEN>/getUpdates<br>`4. Copy `"chat":{"id":-100...}` |
| **`TELEGRAM_BOT_SERVER_IP`**               | IP address of the local Bot API server.                                     | Use the IP where your Bot API server is running.                                                                                                    |
| **`TELEGRAM_BOT_SERVER_PORT`**             | Port of the local Bot API server. Default: **8081**                         | Use the port your Bot API server is configured to run on.                                                                                           |
| **`TELEGRAM_CHAT_THREAD_ID`** *(optional)* | Thread ID inside a forum-style group.                                       | Same method as `TELEGRAM_CHAT_ID` → look for `"message_thread_id":12345` in the update response.                                                    |

---

## 🤖 Android Signing (Optional, for release builds)

| Secret                       | Description                                | How to Get                                                                                 |
| ---------------------------- | ------------------------------------------ | ------------------------------------------------------------------------------------------ |
| **`ANDROID_KEYSTORE_PASS`**  | Password for your `key.keystore`.          | The password you set when creating the keystore. Place `key.keystore` in the project root. |
| **`ANDROID_KEY_ALIAS_NAME`** | Alias name of the key inside the keystore. | The alias you set when creating the key.                                                   |
| **`ANDROID_KEY_ALIAS_PASS`** | Password for the alias.                    | The alias password you set when creating the key.                                          |

⚠️ If missing, release builds are **unsigned** (can’t upload to Play Store).

---

## 🪟 Windows Code Signing (Optional)

| Secret                    | Description                                      | How to Get                                                               |
| ------------------------- | ------------------------------------------------ | ------------------------------------------------------------------------ |
| **`WIN_CERT_PFX_BASE64`** | Code signing certificate in **base64** (`.pfx`). | Convert your `.pfx` file:`bash<br>base64 -w 0 mycert.pfx > cert.b64<br>` |
| **`WIN_CERT_PASSWORD`**   | Password for the `.pfx`.                         | The password you set when creating/exporting the cert.                   |

⚠️ If missing, EXEs run fine but are **unsigned** (SmartScreen warning).

---

## 🍏 iOS / Apple Developer

| Secret            | Description                           | How to Get                                                                                             |
| ----------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| **`IOS_TEAM_ID`** | 10-character Apple Developer Team ID. | Log in to [Apple Developer](https://developer.apple.com/account) → **Membership** → copy your Team ID. |

Required for **iOS/macOS builds** (both test & release).

---

## ✅ Secrets Summary

- **Unity (must choose one method):**
  
  - `UNITY_LICENSE` **(preferred)**
  
  - or `UNITY_EMAIL` + `UNITY_PASSWORD`

- **Telegram (optional, for notifications):**
  
  - `TELEGRAM_BOT_TOKEN`
  
  - `TELEGRAM_CHAT_ID`
  
  - `TELEGRAM_CHAT_THREAD_ID`

- **Android signing (optional, release only):**
  
  - `ANDROID_KEYSTORE_PASS`
  
  - `ANDROID_KEY_ALIAS_NAME`
  
  - `ANDROID_KEY_ALIAS_PASS`

- **Windows signing (optional, release only):**
  
  - `WIN_CERT_PFX_BASE64`
  
  - `WIN_CERT_PASSWORD`

- **iOS (must have for iOS builds):**
  
  - `IOS_TEAM_ID`
