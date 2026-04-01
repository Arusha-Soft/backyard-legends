# 🎮 Unity Project Template

This repository is a **Unity 6000.0.60f1 project template** with built-in **CI/CD pipelines** using [game-ci/unity-builder](https://game.ci/).  
It’s designed for our organization to quickly bootstrap new Unity projects with **automated builds** for **Windows, Android, and iOS**, plus optional **code signing** and **Telegram notifications**.

---

## 🚀 Features

- ✅ Unity **6000.0.60f1** ready

- ✅ GitHub Actions CI/CD for:
  
  - Windows (`.exe`)
  
  - Android (`.apk` / `.aab`)
  
  - iOS (`.ipa`)

- ✅ Automatic **license activation** (via secrets)

- ✅ Optional:
  
  - Android signing (Play Store ready)
  
  - Windows code signing (SmartScreen safe)
  
  - iOS provisioning with Apple Developer account

- ✅ Telegram build notifications (with artifacts attached)

---

## 🔑 Required Setup

Before running builds, configure **repository secrets** in GitHub → **Settings → Secrets and variables → Actions**.

### Unity License (required)

- **Preferred:** `UNITY_LICENSE` → Unity `.ulf` file in base64

- **Alternative:** `UNITY_EMAIL` + `UNITY_PASSWORD` (if no license file)

### Optional Secrets

- **Telegram Notifications:** `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`

- **Android Signing:** `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS_NAME`, `ANDROID_KEY_ALIAS_PASS`

- **Windows Signing:** `WIN_CERT_PFX_BASE64`, `WIN_CERT_PASSWORD`

- **iOS Builds:** `IOS_TEAM_ID`

---

## 🌿 Build Branches

Push or merge into these branches to trigger automated builds:

| Platform             | Branch                  | Output                           |
| -------------------- | ----------------------- | -------------------------------- |
| 🪟 Windows (Test)    | `Build/windows/test`    | Unsigned `.exe`                  |
| 🪟 Windows (Release) | `Build/windows/release` | Signed `.exe` (if cert provided) |
| 🤖 Android (Test)    | `Build/android/test`    | Debug `.apk`                     |
| 🤖 Android (Release) | `Build/android/release` | Signed `.aab` + `.apk`           |
| 🍏 iOS (Test)        | `Build/ios/test`        | Dev `.ipa`                       |
| 🍏 iOS (Release)     | `Build/ios/release`     | App Store `.ipa`                 |

---

## 📦 Build Outputs

- **Artifacts:** Uploaded to GitHub Actions (retained for 1 day)

- **Telegram:**
  
  - Windows builds → split archives sent directly
  
  - Android/iOS → Telegram message with artifact + run link

---

## ✅ Usage Guide

1. Click **“Use this template”** to create a new repo.

2. Set up **Unity license & secrets**.

3. Push code to one of the **build branches**.

4. Collect artifacts from **GitHub Actions** or **Telegram**.

---

## 📖 Documentation

- [🔑 Secrets Setup](./docs/SECRETS.md) — details for Unity, Android, iOS, Windows, Telegram

## 🛠 Unity Version

This template is locked to **Unity 6000.0.60f1**.  
For consistency across the org, always install this version via **Unity Hub**.

---

## 📝 Notes

- Make sure `Custom Keystore` is disabled in **Player Settings → Publishing Settings** before running release Android builds.

- Windows EXEs without a signing cert will still run but may show **SmartScreen warnings**.

- iOS builds require an **Apple Developer Team ID** for provisioning.

---

## 📢 Support

- For CI/CD issues: check [game-ci docs](https://game.ci/docs/github/builder)

- For Unity license problems: regenerate `.ulf` file and update secret

- Internal questions: reach out to the DevOps channel in Slack/Teams
