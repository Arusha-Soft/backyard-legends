# Telegram delivery

The hosted [Telegram Bot API](https://core.telegram.org/bots/api#senddocument) accepts document uploads up to 50 MB. This project limits each part to **45,000,000 bytes** to leave room below that boundary.

- APKs, AABs, and IPAs below the threshold are attached directly.
- Windows players and unsigned iOS Xcode projects are zipped first.
- Larger files are split into `.001`, `.002`, etc. No bytes are dropped.
- Diagnostics are attached alongside the build. SHA-256 checksums remain in GitHub artifacts; Telegram omits checksum and README files and includes no file-joining instructions.
- Files are sent in albums of **2–10 documents**. A remaining single file uses `sendDocument`; more than ten files use additional albums. A Telegram album groups messages visually; the API does not support unlimited attachments in one message.
- The complete existing message format is placed on the first attachment when it fits the 1,024-character caption limit. Longer messages are sent as text, split at 4,096 UTF-16 units without breaking emoji, then followed by attachments.
- Messages use plain text, so names containing quotes, ampersands, angle brackets, or newlines cannot break HTML parsing or the album JSON.
- Forum topic IDs are omitted when not configured.

To restore a split file, download **all** its numbered parts into the same folder. Use **7-Zip → File → Combine files**, or concatenate parts in numeric order, then extract the ZIP or use the original APK/AAB/IPA. Checksums are available in the GitHub artifact for optional verification. These instructions stay in this documentation and are not sent to Telegram.

The sender checks both HTTP status and Telegram's JSON `ok` field. Rate limits honor `retry_after` with bounded retries; transient server errors retry. If Telegram definitively rejects an album with a 400 response, individual document delivery is attempted. Ambiguous network timeouts are reported instead of blindly retrying and creating duplicate albums. Failed delivery attempts get a final text notification when Telegram remains reachable, and the workflow records the failure.

The original project, platform, flavor, repository, branch, commit, actor, build status, duration, artifact, run link, and Arusha Soft footer are retained. A separate store status identifies internal uploads, production drafts, App Store Connect acceptance, or failed uploads. Store success does not claim that an app is publicly available.

GitHub artifact failures do not prevent Telegram delivery. If Telegram is disabled or unavailable, GitHub downloads remain the alternative when their upload succeeded. Full action logs are linked from the notification; sanitized local Unity/Xcode diagnostics are also attached when available.
