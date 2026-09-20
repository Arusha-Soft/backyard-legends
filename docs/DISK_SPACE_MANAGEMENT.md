# Build and artifact storage

GitHub runner disk, GitHub artifact storage, and Telegram upload limits are separate constraints.

## Runner disk

Unity builds run in Linux containers. Before building, the workflow removes unused host .NET, Haskell, and Android installations; the Android SDK used for compilation is inside the Unity container. The workflow does not delete the runner tool cache or macOS system caches.

iOS exports its Xcode project on Linux and transfers a compressed tar archive to a separate macOS job for IPA signing. The macOS job does not install Unity or restore its Library, which leaves more room for Xcode. Temporary signing files and the Xcode archive are removed after export. CocoaPods and Xcode failures retain local logs for diagnostics.

Large games can still exceed hosted-runner disk capacity. Check the runner disk output and build logs before selecting a larger runner or reducing build content.

## GitHub storage

- Artifacts default to **one day** retention, configurable by `ARTIFACT_RETENTION_DAYS` from 1 to 90 (within repository policy).
- Already-compressed binaries/archives use upload compression level 0 to avoid compressing twice.
- Unity Library caching defaults **off**. Enable `CACHE_UNITY_LIBRARY=true` only when the build-time saving justifies cache storage. Cache keys include Unity version, platform, project/package settings, and commit.
- The temporary iOS transfer artifact expires after one day and is deleted after successful iOS delivery. A failed iOS job retains it for retries until expiry. Downloading uses the original artifact ID, so retrying only the macOS job still works when the run-attempt number changes. Only that artifact is deleted; unrelated builds are not pruned.
- A small Linux diagnostics artifact preserves Unity logs before iOS continues on macOS.
- Final GitHub upload errors are reported but do not prevent Telegram from receiving the build. A successful Telegram delivery can keep a build successful despite a GitHub storage failure. Conversely, Telegram errors become warnings when the packaged build is available in GitHub. Losing both delivery routes, failed builds, packaging errors, or failed store uploads still fail the run.

Short retention reduces accumulation; it cannot bypass an exhausted account quota or instantly refresh GitHub's storage accounting. The iOS Linux-to-macOS handoff requires temporary artifact capacity: if that upload fails, signing cannot start and the Linux job reports the failure through Telegram. Free space/delete old artifacts in GitHub before retrying. See [artifact storage documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/remove-workflow-artifacts).

## Telegram

Telegram delivery uses the local build files, independently of the GitHub artifact upload. Files are streamed from disk, split into parts of at most 45 MB when needed, and grouped with both a ten-file limit and a 45 MB combined payload limit. Albums rejected with 400/413 are retried as individual files. See [Telegram limits and reassembly](TELEGRAM_FILE_LIMITS.md).
