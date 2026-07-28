# Big Local Hub — .NET MAUI

A .NET MAUI rendition of the Big Local hub app. The canonical implementation is
the TypeScript monorepo (`northstarapp`, pushed as
[biglocalhub](https://github.com/unifiedbap/biglocalhub)) — React web app, Expo
mobile app, and a shared `packages/core`.

This project talks to **the same Firebase project** (`big-local-ideas`) and the
same Firestore documents. It is not a fork of the data, just a second client.

**Status: walking skeleton.** Auth, company/module gating, Dashboard, and Leads
work end to end. The other ten modules are not built yet.

---

## What's here

| Area | Ported from | Notes |
|---|---|---|
| `Models/Enums.cs` | `packages/core/types.ts` | Modules, lead/job statuses, lead sources |
| `Models/Documents.cs` | `packages/core/types.ts` | Firestore document models |
| `Services/StageLabels.cs` | `packages/core/stageLabels.ts` | Per-company pipeline wording |
| `Services/SessionService.cs` | `packages/core/auth.tsx` | Auth state machine |
| `Services/FirestoreRepository.cs` | `packages/core/hooks.ts` | `useCollection` equivalent |
| `Views/DashboardPage.xaml` | `apps/web/src/pages/Dashboard.tsx` | Stat tiles + recent lists |
| `Views/LeadsPage.xaml` | `apps/web/src/pages/Leads.tsx` | List, filters, add/edit/delete |

### Not ported yet

Calendar, Jobs, Clients, GC & Contractors, Marketing, Cold Call CRM, Time,
Money, Bids, Expenses, Customer Portal, Agenda — plus, within Leads, the
spreadsheet import flow, the cold-call cadence engine, and lead→client→job
conversion. The **More** tab lists the company's enabled modules and marks
which ones this build actually implements.

---

## Architecture notes

**Statuses are strings, not enums.** Values like `"Quote scheduled"` are the
stored contract, shared with the web app, the Expo app, and the Cloud
Functions. They're kept as string constants so nothing can drift. What the user
*sees* is resolved separately through `StageLabels` (company override →
business-type preset → canonical value), exactly as in `stageLabels.ts`.

**Writes are field-level, never whole-document.** The models here map only the
fields this app uses. A lead document also carries cadence state, portal links,
and `importBatchId`; a full-document `SetData` would silently drop them, so
`FirestoreRepository.UpdateAsync` writes named fields only.

**Module gating is real, not cosmetic.** `AppShell` builds its tabs from
`company.enabledModules`, so a module the company doesn't have has no route.

**`importBatchId` matters.** The `notifyOnNewLead` Cloud Function skips leads
carrying it. Leads created from this app deliberately don't set it, so adding
one *will* notify the team — which is correct for a hand-entered lead.

---

## Running it

### iOS — works today

```bash
dotnet build -f net10.0-ios -r iossimulator-arm64
```

Install and launch on a booted simulator:

```bash
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/BigLocalHub.app
```

```bash
xcrun simctl launch booted com.biglocalideas.biglocal
```

### Android — blocked

The Android target is configured but cannot build on this machine yet:

1. **No Android SDK installed.** `dotnet build -f net10.0-android` fails with
   `XA5300`. Install it, or point `AndroidSdkDirectory` at an existing SDK.
2. **No `google-services.json`.** No Android app is registered in the
   `big-local-ideas` Firebase console project (the Expo app was iOS-only).
   Register one for `com.biglocalideas.biglocal`, download the file to
   `Platforms/Android/google-services.json`, and the existing csproj item will
   pick it up.

---

## Gotchas worth knowing

**Bundle id is shared with the Expo app** (`com.biglocalideas.biglocal`), so
this project can reuse that app's `GoogleService-Info.plist` without
registering a second iOS app. The trade-off: the two builds can't be installed
side by side on one device. To run both, change `ApplicationId`, register that
bundle id in Firebase, and drop in the new plist.

**`FirebaseInstallations` must be referenced explicitly.** `FirebaseSessions`
(pulled in by Auth/Firestore) links against it, but no `Plugin.Firebase`
package depends on the Installations binding, so its framework never lands in
the bundle and the app dies at launch with
`dyld: Library not loaded: @rpath/FirebaseInstallations.framework`. Hence the
explicit `AdamE.Firebase.iOS.Installations` reference, version-pinned to match
the other native bindings.

**Use forward slashes in MSBuild `Exists()`.** MSBuild normalizes backslashes
inside `Include`, but *not* inside `Exists()` on macOS — so a Windows-style
path silently evaluates false and quietly omits the file. That cost one
debugging cycle here: the plist was left out of the bundle and only surfaced at
runtime as `FirebaseApp.configure() could not find a valid GoogleService-Info.plist`.

**`GoogleService-Info.plist` is committed.** It's consistent with the Expo app,
which commits the same file, and Firebase iOS config is designed to ship inside
the app binary — the security boundary is Firestore rules, not this file. Move
it out of source control if you'd rather not have it here.
