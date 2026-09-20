# Repository CI setup

Run `scripts/Setup-CI.ps1` from a cloned project on Windows (PowerShell 5.1 or newer). Install [GitHub CLI](https://cli.github.com/) and authenticate with `gh auth login` first. The account needs permission to manage repository Actions secrets and variables. No organization secrets or extra workflow PAT are required.

The first run configures an encrypted shared profile and a project profile. Only **Unity activation and Telegram defaults** are shared. Google Play service accounts, Apple signing certificates/team IDs, App Store Connect API keys, Windows signing certificates, Android keystores, and iOS provisioning profiles are configured and saved **per repository**. `-Configure` edits both profiles; Enter keeps the selected repository's saved values. `-Preview` shows setting names without writing profiles or changing GitHub. If local script execution is restricted, you can invoke this reviewed script for one process with `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup-CI.ps1`.

Profiles are saved under `%LOCALAPPDATA%\ArushaSoft\UnityCI`. Windows DPAPI encrypts secret values for the current Windows account and computer. Those files cannot simply be copied to another account/computer and decrypted there. Preserve the original signing files in your normal credential backup. The setup script does not read secret values back from GitHub.

Running setup updates the documented CI settings to match your selected options, removes disabled managed secrets/variables, and removes legacy secret copies of ordinary settings **after** their variables are uploaded. Unrelated repository settings are untouched. A failed upload stops with an error; the saved profile lets you rerun without entering credentials again. Shared-profile changes take effect in each repository when you rerun setup for it.

## Different clients and store accounts

For example, `MyOrg/client-one-game`, `MyOrg/team-game`, and `MyOrg/client-two-game` each have their own local project profile and GitHub repository settings. Run setup in each clone (or supply `-Repository OWNER/REPO`) and select the Google service-account JSON and Apple signing/API files belonging to that app's publisher. Android and iOS accounts can be configured independently. Choosing `n` disables that publishing feature only for the selected repository; it never falls back to another client's credentials.

You enter the files once for each repository; future runs reuse its encrypted saved values. For another app owned by the same publisher, you can explicitly select the same account files if they have permission for that app. A change to a client's repository profile does not change other repositories. Workflows read the selected repository's secrets and variables, so no account-selection input is needed when running a build.

If you used the older setup script, its shared store/signing credentials are **ignored**, not automatically copied to a repository. The next setup run asks you to configure publishing for that repository before applying GitHub changes. The old encrypted shared values are preserved locally to avoid losing them, but are never used for publishing. Select the appropriate original files again, or choose `n` to disable the feature and remove its managed settings from that repository. Existing GitHub settings change only when you finish running setup for that repository.

## Required Unity activation

| Secret | Source |
| --- | --- |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |
| `UNITY_LICENSE` | **Raw XML contents** of a Personal `.ulf` file; the script reads the file directly |
| `UNITY_SERIAL` | Professional serial, used **instead of** `UNITY_LICENSE` |

Personal activation uses the `.ulf` **plus email and password**. The Windows license is normally `C:\ProgramData\Unity\Unity_lic.ulf`. In Unity Hub, add/activate a Personal license if it does not exist. Professional activation uses serial, email, and password. Do not keep both `UNITY_LICENSE` and `UNITY_SERIAL`; setup removes the unused method. [GameCI activation](https://game.ci/docs/github/activation/).

## Ordinary settings (Actions variables)

| Variable | Default / purpose |
| --- | --- |
| `ANDROID_PACKAGE_NAME` | Application ID; setup suggests the Unity project setting |
| `IOS_BUNDLE_ID` | Bundle ID; setup suggests the Unity project setting |
| `ANDROID_KEY_ALIAS_NAME` | Alias within this app's Android keystore |
| `ANDROID_VERSION_CODE_BASE` | `0`; CI adds `run_number * 100 + run_attempt` |
| `IOS_TEAM_ID` | 10-character Apple Developer Team ID |
| `APP_STORE_CONNECT_KEY_ID` | App Store Connect API key identifier |
| `APP_STORE_CONNECT_ISSUER_ID` | App Store Connect API issuer UUID |
| `TELEGRAM_CHAT_ID` | Destination group/chat ID |
| `TELEGRAM_CHAT_THREAD_ID` | Optional positive forum topic ID; omitted from requests when empty |
| `ARTIFACT_RETENTION_DAYS` | `1`, configurable from 1 to 90 subject to repository limits |
| `CACHE_UNITY_LIBRARY` | `false`; set `true` for faster builds if storage permits |

The script uploads these as variables, not secrets. Workflow choices (platform, test/release, upload) are inputs, not credentials. Product name, semantic version, Unity version, and enabled scenes remain in Unity settings. Change template application IDs to IDs owned by your app before publishing.

## Telegram (optional)

Create a bot with BotFather, add it to your group, and give it permission to send messages/documents. Provide `TELEGRAM_BOT_TOKEN` as a secret and chat/topic IDs as variables. Setup saves the bot and default chat once; each project can override its chat and topic.

With neither token nor chat configured, Telegram is disabled. If only one is configured, delivery reports an incomplete setup. Incorrect permissions, missing topics, and HTTP/API errors are reported rather than treated as successful sends.

## Android signing and Google Play

| Secret | Source |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | Your app upload keystore; setup reads and base64-encodes it |
| `ANDROID_KEYSTORE_PASS` | Keystore password |
| `ANDROID_KEY_ALIAS_PASS` | Key password |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Original service-account key JSON; needed only for store uploads |

Supply the service account authorized in **this app publisher's** Play Console account. It is saved in this repository's profile, not the shared profile.

Keep the keystore outside Git. Do not reuse a template's signing key for every new app. Test APKs can use the debug key; release AABs and store uploads require all signing settings. The build script applies keystore settings itself, so there is no need to manually toggle Unity's Custom Keystore setting for CI.

For Google Play uploads:

1. Create the app in Play Console and complete required account/app setup. Bootstrap a new app with its first manual upload where required by the publishing API.
2. Enable the Google Play Android Developer API for the service account's Google Cloud project.
3. Grant the service account access to the app in Play Console, including the permissions needed to upload builds and manage the chosen testing/production track.
4. Use the upload key accepted for that app, and set the exact `ANDROID_PACKAGE_NAME`.
5. Enable **Upload to store** when running the workflow.

Test uploads create a completed release on **internal testing**. Release uploads create a **production draft**, with changes not submitted for review. Draft apps may require an initial manual release/setup before a completed internal-track upload is accepted. Errors such as permission denial, duplicate version code, wrong signing key, or SDK-policy rejection appear in the Google Play action log; Telegram reports the store failure and links the run. [Google Play upload action](https://github.com/r0adkll/upload-google-play).

For an existing app, select a version-code base higher than its current highest code. Each run adds 100 per run number plus the retry number (1–99); the result must remain within Google's 2,100,000,000 limit. Concurrent runs of the same platform are serialized. Changing branches does not create a separate version-code sequence.

## iOS signing and App Store Connect

| Secret | Source |
| --- | --- |
| `IOS_CERT_P12_BASE64` | Exported signing certificate(s) **with private keys**, encoded by setup |
| `IOS_CERT_PASSWORD` | Password protecting the P12 |
| `IOS_PROFILE_DEVELOPMENT_BASE64` | Development `.mobileprovision` for installable test IPAs |
| `IOS_PROFILE_APPSTORE_BASE64` | App Store distribution `.mobileprovision` for release IPAs and all store uploads |
| `APP_STORE_CONNECT_PRIVATE_KEY` | Original App Store Connect `.p8` API private key |

Use this app publisher's Apple team, certificates, profiles, and App Store Connect API key. Setup saves the full set in this repository's profile; it does not inherit an Apple team or key from another repository.

For local device tests, the P12 must include an Apple Development identity matching the development profile; the profile must include the test devices. For release/TestFlight, use an Apple Distribution identity and an App Store distribution profile. A P12 can contain both identities and private keys. Team ID, API key ID, and issuer ID are variables configured by the same script.

The workflow checks profile expiration, team, bundle ID, distribution type, and whether the P12 contains a matching certificate/private key. A temporary keychain is created on macOS, and the profile is applied only to the app target (not UnityFramework). Credentials and the temporary keychain are cleaned up after export. If an iOS Podfile is generated, CocoaPods installation runs before archiving.

Without the selected development/distribution profile, a build without store upload produces an **Xcode project ZIP**, not an installable IPA. Adding a partial signing setup or requesting a store upload without the required signing files produces an error. App extensions require an additional profile mapping; this template handles the standard Unity app target.

For uploads, create the app and matching bundle ID in App Store Connect. Create a team API key with sufficient app upload access, download its `.p8`, and supply its key and issuer IDs. The workflow uploads the distribution IPA through [Apple's upload action](https://github.com/Apple-Actions/upload-testflight-build/tree/v3). Upload success means Apple accepted the upload; processing, export compliance, tester assignment, external beta review, and public App Store submission remain in App Store Connect.

Both test and release uploads use the App Store profile and non-development build settings. CI sets the iOS build number to `run_number.run_attempt`; keep it above existing builds for the app version if migrating from another pipeline. Bump the product version in Unity when required for a new App Store version. Xcode and Unity must support the SDK currently required by Apple.

## Windows signing (optional)

Provide `WIN_CERT_PFX_BASE64` and `WIN_CERT_PASSWORD`; setup reads the PFX and encodes it. Release builds sign `Game.exe` using Authenticode with SHA-256 and an RFC 3161 timestamp, then verify the result. Signing or verification errors fail the build. Without a certificate, Windows release builds remain unsigned. Test builds are unsigned development players.

This implementation supports an exportable PFX. Hardware/cloud signing services need their provider-specific integration. Signing does not guarantee that Windows SmartScreen will immediately trust a new application.

## Validation and first run

Start with a test build and store upload disabled. Confirm the GitHub download, Telegram destination/topic, and installability. Then run a signed build; finally enable a test store upload. Store credentials alone do not trigger deployment: the manual upload checkbox must be selected.

Each GitHub secret is limited to 48 KB; setup checks the UTF-8/base64 payload size before uploading. It never prints credential values or sends them in command-line arguments. [GitHub secret documentation](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets).

If a store accepted an upload but later delivery failed, start a new full build for a new build/version code instead of resubmitting the same binary. Failed iOS signing jobs can reuse their transfer artifact for one day; after expiry, rerun the full build to regenerate it.
