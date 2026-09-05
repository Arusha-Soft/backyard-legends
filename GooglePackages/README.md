# Firebase Unity packages (local UPM)

Pinned Firebase Unity SDK **13.16.0** + External Dependency Manager **1.2.186**.

Referenced from [`Packages/manifest.json`](../Packages/manifest.json) via `file:` paths.

Re-download if missing:

```powershell
$ver = "13.16.0"
$edmVer = "1.2.186"
$base = "https://dl.google.com/games/registry/unity"
curl.exe -L -o "com.google.external-dependency-manager-$edmVer.tgz" "$base/com.google.external-dependency-manager/com.google.external-dependency-manager-$edmVer.tgz"
curl.exe -L -o "com.google.firebase.app-$ver.tgz" "$base/com.google.firebase.app/com.google.firebase.app-$ver.tgz"
curl.exe -L -o "com.google.firebase.auth-$ver.tgz" "$base/com.google.firebase.auth/com.google.firebase.auth-$ver.tgz"
curl.exe -L -o "com.google.firebase.firestore-$ver.tgz" "$base/com.google.firebase.firestore/com.google.firebase.firestore-$ver.tgz"
```
