# 🎮 Unity Project Template

Unity **6000.0.60f1** with manual GitHub Actions builds for **Android, iOS, and Windows**. Builds and diagnostics are available in GitHub and optionally Telegram. Store uploads are opt-in for each run.

## Create a project

1. Use this template to create a repository, then clone it.
2. Install [GitHub CLI](https://cli.github.com/) and run `gh auth login` with an account that can manage the repository's Actions settings.
3. From the project folder, run:

   ```powershell
   .\scripts\Setup-CI.ps1
   ```

The script asks for shared Unity credentials and Telegram defaults once and saves them encrypted with Windows DPAPI under `%LOCALAPPDATA%\ArushaSoft\UnityCI`. **Store accounts and signing credentials are saved separately for each repository.** A client's project can use that client's Google Play service account and Apple team, while your team's games use your own accounts. Each new repository asks for its own publishing setup; reruns reuse only that repository's saved choices. This uses **repository secrets**, so it works for private repositories without paid organization-secret support.

Secret values are sent to GitHub through stdin. The script puts ordinary settings in **Actions variables**, handles file encoding, and removes obsolete secret copies of migrated variables. It manages only the documented CI setting names. Never put credentials into the template.

Useful options:

```powershell
# Review names without changing local profiles or GitHub settings.
.\scripts\Setup-CI.ps1 -Preview

# Edit saved settings; Enter keeps existing values.
.\scripts\Setup-CI.ps1 -Configure

# Configure another repository with its own store accounts and signing files.
.\scripts\Setup-CI.ps1 -Repository Arusha-Soft/my-game
```

See [credential and store setup](docs/SECRETS.md) for the required files and permissions.

## Run a build

Open **Repository → Actions → Build → Run workflow**. Select:

- **Branch:** the normal branch containing the code to build, such as `main` or `develop`.
- **Platform:** Android, iOS, Windows, or All.
- **Build type:** test or release.
- **Upload to store:** off by default; enable only when you want a store submission.

The workflow files must be present on the default branch for the manual button to appear, and the selected branch must contain this CI implementation. No push/tag triggers or special build branches are required. Existing remote branches are not deleted by these changes.

| Platform | Test, upload off | Release, upload off | Upload to store enabled |
| --- | --- | --- | --- |
| Android | Development APK; debug key unless your app key is configured | Signed release AAB; app signing setup required | Signed, non-debuggable AAB. Test → internal testing; release → production **draft** |
| iOS | Development IPA with a development profile; otherwise Xcode project ZIP | App Store distribution IPA with a distribution profile; otherwise Xcode project ZIP | Distribution IPA uploaded to App Store Connect/TestFlight, for either build type |
| Windows | Development player ZIP | Release player ZIP; signed and verified if a PFX is configured | No Windows store upload; normal build only |

Store testing uses a distribution build rather than Unity's development/debugging flags. iOS uploads do not automatically submit an app for public review or enable external testers. Google Play production releases remain drafts. Store processing, account approvals, and listing requirements still apply.

Unity version and product version come from `ProjectSettings`. Build filenames include platform, build type, run number, and retry number. Android version codes increase across runs and retries; use `ANDROID_VERSION_CODE_BASE` when adopting CI for an existing published app. iOS build numbers use `run.attempt`.

Windows currently cross-builds with Mono on Linux. Switching Windows to IL2CPP requires a Windows runner/toolchain. iOS exports on Linux, then transfers to macOS for signing, which also supports the Personal `.ulf` activation used by GameCI on Linux.

## Downloads, Telegram, and failures

Each platform publishes its build and a diagnostics ZIP. GitHub artifacts also include SHA-256 checksums; Telegram omits checksum and README files and file-joining instructions. Default GitHub retention is **one day**, and Unity Library caching is disabled by default to conserve storage. Temporary iOS transfer artifacts are deleted after successful iOS delivery; a failed job retains its transfer for retry until expiry.

Telegram preserves the existing project/platform/flavor/branch/commit/status/footer format and adds store status. Files are grouped into albums of up to ten; large files are split into parts below 45 MB. Longer messages are sent intact as text when they exceed caption limits. See [Telegram delivery](docs/TELEGRAM_FILE_LIMITS.md).

Build success, store upload, and artifact delivery are reported separately. A failed store upload fails the run while still delivering the successfully built binary. GitHub storage errors do not block Telegram delivery. If both delivery routes are unavailable, the run fails instead of silently losing the output. The iOS transfer still requires temporary GitHub artifact capacity. See [storage management](docs/DISK_SPACE_MANAGEMENT.md).

## Maintain and validate

`build.yml` is the small manual entry point; `build-platform.yml` contains the reusable build implementation. Helpers live in `.github/scripts`, and Unity builds use `Assets/Editor/BuildScript.cs`. This template is self-contained; creating another central repository is not required.

Offline checks:

```powershell
node --test tests/ci.test.mjs
.\tests\setup-ci.tests.ps1
actionlint
```

Node tests use mocked Telegram responses. The PowerShell test creates dummy encrypted credentials and a fake GitHub executable; it never contacts GitHub or a store. Real signing and store acceptance must be checked with the app's credentials in an actual manual build.
