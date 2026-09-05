# Place Firebase client config files here (gitignored)

- `google-services.json` — Android (`com.tammrow.backyardlegends`)
- `GoogleService-Info.plist` — iOS (`com.tammrow.backyardlegends`)

Project: **backyard-legends**

Refresh with Firebase CLI (from repo root):

```powershell
firebase use backyard-legends
firebase apps:sdkconfig ANDROID 1:699148197424:android:799a08f0b1b0bbc2ca17f5 --out Assets/google-services.json
firebase apps:sdkconfig IOS 1:699148197424:ios:65a625148c87e6ecca17f5 --out Assets/GoogleService-Info.plist
```

See [docs/SECRETS.md](../docs/SECRETS.md).
